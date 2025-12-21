using Neo.Orleans.Interfaces;

namespace Neo.Orleans.States;

/// <summary>
/// Persistent state for ConsensusGrain.
/// Tracks dBFT consensus state machine.
/// </summary>
[GenerateSerializer]
public class ConsensusGrainState
{
    /// <summary>
    /// Current view number (increments on view change).
    /// </summary>
    [Id(0)] public byte ViewNumber { get; set; }

    /// <summary>
    /// Current block index being proposed.
    /// </summary>
    [Id(1)] public uint BlockIndex { get; set; }

    /// <summary>
    /// Current consensus phase.
    /// </summary>
    [Id(2)] public ConsensusPhase Phase { get; set; } = ConsensusPhase.Initial;

    /// <summary>
    /// Whether consensus is currently running.
    /// </summary>
    [Id(3)] public bool IsRunning { get; set; }

    /// <summary>
    /// Index of this node in the validator list.
    /// </summary>
    [Id(4)] public int MyIndex { get; set; } = -1;

    /// <summary>
    /// Total number of validators.
    /// </summary>
    [Id(5)] public int ValidatorCount { get; set; } = 7;

    /// <summary>
    /// Received prepare request hashes by validator index.
    /// </summary>
    [Id(6)] public Dictionary<int, byte[]> PrepareRequestPayloads { get; set; } = new();

    /// <summary>
    /// Received prepare response signatures by validator index.
    /// </summary>
    [Id(7)] public Dictionary<int, byte[]> PrepareResponsePayloads { get; set; } = new();

    /// <summary>
    /// Received commit signatures by validator index.
    /// </summary>
    [Id(8)] public Dictionary<int, byte[]> CommitPayloads { get; set; } = new();

    /// <summary>
    /// Timestamp when current view started.
    /// </summary>
    [Id(9)] public long ViewStartTime { get; set; }

    /// <summary>
    /// Block hash being proposed in current view.
    /// </summary>
    [Id(10)] public byte[] ProposedBlockHash { get; set; } = Array.Empty<byte>();
}
