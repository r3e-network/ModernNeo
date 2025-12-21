// Copyright (C) 2015-2025 The Neo Project.
//
// DbftStateMachine.cs file belongs to the neo project and is free
// software distributed under the MIT software license.

namespace Neo.Consensus;

/// <summary>
/// dBFT (Delegated Byzantine Fault Tolerance) consensus state machine.
/// Implements the core consensus algorithm for Neo blockchain.
/// </summary>
public sealed class DbftStateMachine : IConsensusService, IDisposable
{
    private readonly DbftOptions _options;
    private readonly IConsensusContext _context;
    private readonly Timer _timer;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private ConsensusPhase _phase = ConsensusPhase.Initial;
    private byte _viewNumber;
    private uint _blockIndex;
    private bool _disposed;

    /// <inheritdoc/>
    public bool IsRunning { get; private set; }

    /// <inheritdoc/>
    public IConsensusContext? Context => _context;

    /// <inheritdoc/>
    public event EventHandler<ConsensusMessageEventArgs>? MessageGenerated;

    /// <inheritdoc/>
    public event EventHandler<BlockReadyEventArgs>? BlockReady;

    /// <inheritdoc/>
    public event EventHandler<ViewChangedEventArgs>? ViewChanged;

    /// <summary>
    /// Event raised when phase changes.
    /// </summary>
    public event EventHandler<PhaseChangedEventArgs>? PhaseChanged;

    /// <summary>
    /// Creates a new dBFT state machine.
    /// </summary>
    public DbftStateMachine(IConsensusContext context, DbftOptions? options = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _options = options ?? new DbftOptions();
        _timer = new Timer(OnTimeout, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRunning)
            return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            IsRunning = true;
            _blockIndex = _context.BlockIndex;
            _viewNumber = 0;

            await InitializeRoundAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
            return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            IsRunning = false;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            SetPhase(ConsensusPhase.Initial);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task OnMessageAsync(IConsensusMessage message, byte[] senderPublicKey)
    {
        if (!IsRunning)
            return;

        await _lock.WaitAsync();
        try
        {
            switch (message.Type)
            {
                case ConsensusMessageType.PrepareRequest:
                    await HandlePrepareRequestAsync(message, senderPublicKey);
                    break;
                case ConsensusMessageType.PrepareResponse:
                    await HandlePrepareResponseAsync(message, senderPublicKey);
                    break;
                case ConsensusMessageType.Commit:
                    await HandleCommitAsync(message, senderPublicKey);
                    break;
                case ConsensusMessageType.ChangeView:
                    await HandleChangeViewAsync(message, senderPublicKey);
                    break;
                case ConsensusMessageType.RecoveryRequest:
                    await HandleRecoveryRequestAsync(message, senderPublicKey);
                    break;
                case ConsensusMessageType.RecoveryMessage:
                    await HandleRecoveryMessageAsync(message, senderPublicKey);
                    break;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task OnBlockPersistedAsync(uint blockIndex)
    {
        await _lock.WaitAsync();
        try
        {
            if (blockIndex >= _blockIndex)
            {
                _blockIndex = blockIndex + 1;
                _viewNumber = 0;
                await InitializeRoundAsync();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public Task<ConsensusStateSnapshot> GetStateAsync()
    {
        return Task.FromResult(new ConsensusStateSnapshot(
            BlockIndex: _blockIndex,
            ViewNumber: _viewNumber,
            Phase: _phase,
            IsPrimary: IsPrimary(),
            ValidatorCount: _context.ValidatorCount,
            PrepareResponseCount: _context.PrepareResponseCount,
            CommitCount: _context.CommitCount,
            IsRunning: IsRunning));
    }

    private async Task InitializeRoundAsync(CancellationToken cancellationToken = default)
    {
        _context.Reset(_blockIndex, _viewNumber);
        SetPhase(ConsensusPhase.Initial);

        if (IsPrimary())
        {
            // Primary: wait a bit then send PrepareRequest
            SetPhase(ConsensusPhase.Primary);
            _timer.Change(_options.BlockInterval, Timeout.InfiniteTimeSpan);
        }
        else
        {
            // Backup: wait for PrepareRequest
            SetPhase(ConsensusPhase.Backup);
            _timer.Change(_options.ViewChangeTimeout, Timeout.InfiniteTimeSpan);
        }
    }

    private async void OnTimeout(object? state)
    {
        if (!IsRunning)
            return;

        await _lock.WaitAsync();
        try
        {
            switch (_phase)
            {
                case ConsensusPhase.Primary:
                    // Time to send PrepareRequest
                    await SendPrepareRequestAsync();
                    break;

                case ConsensusPhase.Backup:
                case ConsensusPhase.RequestReceived:
                case ConsensusPhase.ResponseSent:
                    // Timeout waiting - initiate view change
                    await InitiateViewChangeAsync(ViewChangeReason.Timeout);
                    break;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SendPrepareRequestAsync()
    {
        if (_phase != ConsensusPhase.Primary)
            return;

        var message = _context.CreatePrepareRequest();
        if (message != null)
        {
            SetPhase(ConsensusPhase.RequestSent);
            MessageGenerated?.Invoke(this, new ConsensusMessageEventArgs(message));

            // Also process our own PrepareRequest
            _context.OnPrepareRequestReceived();

            // Set timeout for responses
            _timer.Change(_options.ViewChangeTimeout, Timeout.InfiniteTimeSpan);
        }
    }

    private async Task HandlePrepareRequestAsync(IConsensusMessage message, byte[] senderPublicKey)
    {
        if (_phase != ConsensusPhase.Backup)
            return;

        // Verify sender is the expected primary
        var expectedPrimary = GetPrimaryIndex();
        var senderIndex = _context.GetValidatorIndex(senderPublicKey);

        if (senderIndex != expectedPrimary)
            return;

        // Validate the block proposal
        if (!_context.ValidatePrepareRequest(message))
        {
            await InitiateViewChangeAsync(ViewChangeReason.InvalidBlock);
            return;
        }

        _context.OnPrepareRequestReceived();
        SetPhase(ConsensusPhase.RequestReceived);

        // Send PrepareResponse
        var response = _context.CreatePrepareResponse();
        if (response != null)
        {
            SetPhase(ConsensusPhase.ResponseSent);
            MessageGenerated?.Invoke(this, new ConsensusMessageEventArgs(response));
        }

        // Reset timeout
        _timer.Change(_options.ViewChangeTimeout, Timeout.InfiniteTimeSpan);

        await CheckForCommitAsync();
    }

    private async Task HandlePrepareResponseAsync(IConsensusMessage message, byte[] senderPublicKey)
    {
        if (_phase < ConsensusPhase.RequestSent && _phase != ConsensusPhase.ResponseSent)
            return;

        var senderIndex = _context.GetValidatorIndex(senderPublicKey);
        if (senderIndex < 0)
            return;

        _context.OnPrepareResponseReceived(senderIndex, message);

        await CheckForCommitAsync();
    }

    private async Task CheckForCommitAsync()
    {
        // Check if we have M = 2f + 1 PrepareResponses
        var m = _context.Validators!.Count - (_context.Validators.Count - 1) / 3;

        if (_context.PrepareResponseCount >= m && _phase < ConsensusPhase.CommitSent)
        {
            // Send Commit
            var commit = _context.CreateCommit();
            if (commit != null)
            {
                SetPhase(ConsensusPhase.CommitSent);
                MessageGenerated?.Invoke(this, new ConsensusMessageEventArgs(commit));
                _context.OnCommitReceived(_context.MyIndex, commit);
            }

            await CheckForBlockAsync();
        }
    }

    private async Task HandleCommitAsync(IConsensusMessage message, byte[] senderPublicKey)
    {
        var senderIndex = _context.GetValidatorIndex(senderPublicKey);
        if (senderIndex < 0)
            return;

        _context.OnCommitReceived(senderIndex, message);

        await CheckForBlockAsync();
    }

    private async Task CheckForBlockAsync()
    {
        // Check if we have M commits
        var m = _context.Validators!.Count - (_context.Validators.Count - 1) / 3;

        if (_context.CommitCount >= m && _phase < ConsensusPhase.BlockSent)
        {
            SetPhase(ConsensusPhase.BlockSent);

            var blockHash = _context.GetBlockHash();
            var signatures = _context.GetCommitSignatures();

            if (blockHash != null && signatures != null)
            {
                BlockReady?.Invoke(this, new BlockReadyEventArgs(_blockIndex, blockHash, signatures));
            }
        }
    }

    private async Task HandleChangeViewAsync(IConsensusMessage message, byte[] senderPublicKey)
    {
        var senderIndex = _context.GetValidatorIndex(senderPublicKey);
        if (senderIndex < 0)
            return;

        _context.OnChangeViewReceived(senderIndex, message);

        // Check if we have enough ChangeView messages
        var f = (_context.Validators!.Count - 1) / 3;
        var changeViewCount = _context.GetChangeViewCount((byte)(_viewNumber + 1));

        if (changeViewCount >= f + 1)
        {
            await PerformViewChangeAsync();
        }
    }

    private async Task InitiateViewChangeAsync(ViewChangeReason reason)
    {
        var changeView = _context.CreateChangeView((byte)(_viewNumber + 1));
        if (changeView != null)
        {
            MessageGenerated?.Invoke(this, new ConsensusMessageEventArgs(changeView));
        }

        // Check if we already have enough
        var f = (_context.Validators!.Count - 1) / 3;
        var changeViewCount = _context.GetChangeViewCount((byte)(_viewNumber + 1));

        if (changeViewCount >= f + 1)
        {
            await PerformViewChangeAsync();
        }
        else
        {
            // Wait for more ChangeView messages
            _timer.Change(_options.ViewChangeTimeout, Timeout.InfiniteTimeSpan);
        }
    }

    private async Task PerformViewChangeAsync()
    {
        var oldView = _viewNumber;
        _viewNumber++;

        ViewChanged?.Invoke(this, new ViewChangedEventArgs(oldView, _viewNumber, ViewChangeReason.ViewChangeRequest));

        await InitializeRoundAsync();
    }

    private async Task HandleRecoveryRequestAsync(IConsensusMessage message, byte[] senderPublicKey)
    {
        // Send recovery message with current state
        var recovery = _context.CreateRecoveryMessage();
        if (recovery != null)
        {
            MessageGenerated?.Invoke(this, new ConsensusMessageEventArgs(recovery));
        }
    }

    private async Task HandleRecoveryMessageAsync(IConsensusMessage message, byte[] senderPublicKey)
    {
        // Apply recovery state
        _context.ApplyRecoveryMessage(message);

        // Re-evaluate current state
        await CheckForCommitAsync();
        await CheckForBlockAsync();
    }

    private bool IsPrimary()
    {
        return _context.MyIndex == GetPrimaryIndex();
    }

    private int GetPrimaryIndex()
    {
        var validatorCount = _context.Validators?.Count ?? 1;
        return (int)((_blockIndex + _viewNumber) % (uint)validatorCount);
    }

    private void SetPhase(ConsensusPhase newPhase)
    {
        if (_phase != newPhase)
        {
            var oldPhase = _phase;
            _phase = newPhase;
            PhaseChanged?.Invoke(this, new PhaseChangedEventArgs(oldPhase, newPhase));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _timer.Dispose();
        _lock.Dispose();
    }
}

/// <summary>
/// Configuration options for dBFT.
/// </summary>
public sealed class DbftOptions
{
    /// <summary>
    /// Block generation interval. Default: 15 seconds.
    /// </summary>
    public TimeSpan BlockInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Timeout before initiating view change. Default: 30 seconds.
    /// </summary>
    public TimeSpan ViewChangeTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum view changes before giving up. Default: 10.
    /// </summary>
    public int MaxViewChanges { get; set; } = 10;
}

/// <summary>
/// Event args for phase change.
/// </summary>
public sealed class PhaseChangedEventArgs : EventArgs
{
    public ConsensusPhase OldPhase { get; }
    public ConsensusPhase NewPhase { get; }

    public PhaseChangedEventArgs(ConsensusPhase oldPhase, ConsensusPhase newPhase)
    {
        OldPhase = oldPhase;
        NewPhase = newPhase;
    }
}
