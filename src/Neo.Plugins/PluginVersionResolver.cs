// Copyright (C) 2015-2025 The Neo Project.
//
// PluginVersionResolver.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Neo.Plugins
{
    /// <summary>
    /// Provides semantic version parsing and version range resolution compatible
    /// with common SemVer operators (^, ~, *, exact, inequalities) and NuGet-style ranges.
    /// </summary>
    public static class PluginVersionResolver
    {
        /// <summary>
        /// Determines whether a version satisfies the specified range expression.
        /// </summary>
        /// <param name="version">Version to test.</param>
        /// <param name="versionRange">Range expression (e.g., "&gt;=1.0.0 &lt;2.0.0", "^1.2.3", "~1.2", "1.*", "[1.0,2.0)").</param>
        /// <returns><c>true</c> if compatible; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when inputs are null.</exception>
        public static bool IsCompatible(Version version, string versionRange)
        {
            if (version is null) throw new ArgumentNullException(nameof(version));
            if (string.IsNullOrWhiteSpace(versionRange)) throw new ArgumentNullException(nameof(versionRange));
            return ParseVersionRange(versionRange).Matches(version);
        }

        /// <summary>
        /// Parses a version range expression to a <see cref="VersionRange"/> that can
        /// be evaluated against <see cref="Version"/> instances.
        /// </summary>
        /// <param name="range">Range expression.</param>
        /// <returns>A parsed <see cref="VersionRange"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="range"/> is null.</exception>
        public static VersionRange ParseVersionRange(string range)
        {
            if (range is null) throw new ArgumentNullException(nameof(range));
            range = range.Trim();
            if (range.Length == 0) return VersionRange.All;

            // Split OR groups using "||"
            var orGroups = range.Split(new[] { "||" }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (orGroups.Length == 1)
            {
                return new VersionRange(new[] { ParseAndGroup(orGroups[0]) });
            }

            var groups = new List<ConstraintGroup>(orGroups.Length);
            foreach (var g in orGroups)
                groups.Add(ParseAndGroup(g));

            return new VersionRange(groups);
        }

        private static ConstraintGroup ParseAndGroup(string expr)
        {
            expr = expr.Trim();
            if (expr.Length == 0) return new ConstraintGroup(Array.Empty<Constraint>());

            // NuGet bracket range: [1.0,2.0), (1.0, 2.0], [1.0, ), (,2.0]
            if ((expr.StartsWith("[", StringComparison.Ordinal) || expr.StartsWith("(", StringComparison.Ordinal)) &&
                (expr.EndsWith("]", StringComparison.Ordinal) || expr.EndsWith(")", StringComparison.Ordinal)))
            {
                return ParseNuGetBracket(expr);
            }

            // Hyphen range: "1.0.0 - 2.0.0"
            var hyphenIdx = expr.IndexOf(" - ", StringComparison.Ordinal);
            if (hyphenIdx > 0)
            {
                var left = expr[..hyphenIdx].Trim();
                var right = expr[(hyphenIdx + 3)..].Trim();
                var lower = new Constraint(Comparator.GreaterOrEqual, ParseToVersion(left));
                var upper = new Constraint(Comparator.LessOrEqual, ParseToVersion(right));
                return new ConstraintGroup(new[] { lower, upper });
            }

            // Tokenize on whitespace or chained operators (e.g., ">=1.0.0 <2.0.0")
            var tokens = expr.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var constraints = new List<Constraint>(tokens.Length);
            foreach (var token in tokens)
            {
                constraints.AddRange(ParseTokenToConstraints(token));
            }
            return new ConstraintGroup(constraints);
        }

        private static ConstraintGroup ParseNuGetBracket(string expr)
        {
            // Strip brackets/parentheses and split on comma
            var includeLower = expr.StartsWith("[", StringComparison.Ordinal);
            var includeUpper = expr.EndsWith("]", StringComparison.Ordinal);

            var inner = expr.Substring(1, expr.Length - 2);
            var parts = inner.Split(',', StringSplitOptions.TrimEntries);

            var constraints = new List<Constraint>(2);
            if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]) && parts[0] != "*")
            {
                var lower = ParseToVersion(parts[0]);
                constraints.Add(new Constraint(includeLower ? Comparator.GreaterOrEqual : Comparator.Greater, lower));
            }
            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) && parts[1] != "*")
            {
                var upper = ParseToVersion(parts[1]);
                constraints.Add(new Constraint(includeUpper ? Comparator.LessOrEqual : Comparator.Less, upper));
            }

            return new ConstraintGroup(constraints);
        }

        private static IEnumerable<Constraint> ParseTokenToConstraints(string token)
        {
            token = token.Trim();
            if (token.Length == 0) yield break;

            if (token == "*")
            {
                yield break; // no constraints
            }

            // Wildcards: 1.* or 1.2.*
            if (token.Contains('*', StringComparison.Ordinal))
            {
                var baseVersion = token.Replace("*", "0", StringComparison.Ordinal);
                var v = ParseToVersion(baseVersion);
                yield return new Constraint(Comparator.GreaterOrEqual, v);

                // Determine next boundary
                if (CountDots(token) == 2)
                {
                    // 1.2.* => < 1.3.0
                    var upper = new Version(v.Major, v.Minor + 1, 0);
                    yield return new Constraint(Comparator.Less, upper);
                }
                else
                {
                    // 1.* => < 2.0.0
                    var upper = new Version(v.Major + 1, 0, 0);
                    yield return new Constraint(Comparator.Less, upper);
                }
                yield break;
            }

            // Caret ^1.2.3
            if (token.StartsWith("^", StringComparison.Ordinal))
            {
                var v = ParseToVersion(token[1..].Trim());
                yield return new Constraint(Comparator.GreaterOrEqual, v);
                Version upper;
                if (v.Major > 0)
                    upper = new Version(v.Major + 1, 0, 0);
                else if (v.Minor > 0)
                    upper = new Version(0, v.Minor + 1, 0);
                else
                    upper = new Version(0, 0, v.Build + 1);
                yield return new Constraint(Comparator.Less, upper);
                yield break;
            }

            // Tilde ~1.2.3 or ~1.2 or ~1
            if (token.StartsWith("~", StringComparison.Ordinal))
            {
                var raw = token[1..].Trim();
                var v = ParseToVersion(raw);
                yield return new Constraint(Comparator.GreaterOrEqual, v);

                Version upper;
                if (raw.Count(c => c == '.') >= 2)
                {
                    // ~1.2.3 => < 1.3.0
                    upper = new Version(v.Major, v.Minor + 1, 0);
                }
                else if (raw.Count(c => c == '.') == 1)
                {
                    // ~1.2 => < 1.3.0
                    upper = new Version(v.Major, v.Minor + 1, 0);
                }
                else
                {
                    // ~1 => < 2.0.0
                    upper = new Version(v.Major + 1, 0, 0);
                }
                yield return new Constraint(Comparator.Less, upper);
                yield break;
            }

            // Inequalities and equals
            if (StartsWithAny(token, out var op, new[] { ">=", "<=", ">", "<", "==", "=" }))
            {
                var raw = token.Substring(op.Length).Trim();
                var v = ParseToVersion(raw);
                yield return new Constraint(op switch
                {
                    ">=" => Comparator.GreaterOrEqual,
                    "<=" => Comparator.LessOrEqual,
                    ">" => Comparator.Greater,
                    "<" => Comparator.Less,
                    "==" => Comparator.Equal,
                    "=" => Comparator.Equal,
                    _ => throw new InvalidOperationException("Unexpected comparator.")
                }, v);
                yield break;
            }

            // Plain version => exact match
            var exact = ParseToVersion(token);
            yield return new Constraint(Comparator.Equal, exact);
        }

        private static bool StartsWithAny(string token, out string matched, string[] ops)
        {
            foreach (var op in ops.OrderByDescending(s => s.Length))
            {
                if (token.StartsWith(op, StringComparison.Ordinal))
                {
                    matched = op;
                    return true;
                }
            }
            matched = string.Empty;
            return false;
        }

        private static int CountDots(string s) => s.Count(c => c == '.');

        /// <summary>
        /// Parses a string (SemVer-like) into a <see cref="Version"/> ignoring any pre-release/build metadata.
        /// Missing components are treated as zeros.
        /// </summary>
        private static Version ParseToVersion(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException("Version string cannot be null or empty.", nameof(s));

            // Remove pre-release or build metadata (e.g., 1.2.3-alpha+001)
            var core = s.Split(new[] { '-', '+' }, 2, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            var parts = core.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            static int ParsePartOrZero(string part)
                => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

            int major = 0, minor = 0, patch = 0;
            if (parts.Length >= 1) major = ParsePartOrZero(parts[0]);
            if (parts.Length >= 2) minor = ParsePartOrZero(parts[1]);
            if (parts.Length >= 3) patch = ParsePartOrZero(parts[2]);

            return new Version(major, minor, patch);
        }
    }

    /// <summary>
    /// Represents a parsed version range composed of one or more OR-groups,
    /// each group being a set of constraints combined with AND semantics.
    /// </summary>
    public sealed class VersionRange
    {
        private readonly IReadOnlyList<ConstraintGroup> _groups;

        /// <summary>
        /// A range that matches all versions.
        /// </summary>
        public static VersionRange All { get; } = new VersionRange(new[] { new ConstraintGroup(Array.Empty<Constraint>()) });

        /// <summary>
        /// Initializes a new instance of the <see cref="VersionRange"/> class.
        /// </summary>
        /// <param name="groups">OR groups of constraints.</param>
        public VersionRange(IEnumerable<ConstraintGroup> groups)
        {
            _groups = groups?.ToArray() ?? Array.Empty<ConstraintGroup>();
        }

        /// <summary>
        /// Determines whether the specified version matches this range.
        /// </summary>
        public bool Matches(Version version)
        {
            if (_groups.Count == 0) return true;
            foreach (var group in _groups)
            {
                if (group.Matches(version)) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Represents an AND-set of constraints.
    /// </summary>
    public sealed class ConstraintGroup
    {
        private readonly IReadOnlyList<Constraint> _constraints;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConstraintGroup"/> class.
        /// </summary>
        /// <param name="constraints">The constraints in the group.</param>
        public ConstraintGroup(IEnumerable<Constraint> constraints)
        {
            _constraints = constraints?.ToArray() ?? Array.Empty<Constraint>();
        }

        /// <summary>
        /// Determines whether the specified version matches all constraints.
        /// </summary>
        public bool Matches(Version version)
        {
            foreach (var c in _constraints)
            {
                if (!c.IsMatch(version)) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Comparison operator used in version constraints.
    /// </summary>
    public enum Comparator
    {
        /// <summary>Strictly less than.</summary>
        Less,
        /// <summary>Less than or equal to.</summary>
        LessOrEqual,
        /// <summary>Strictly greater than.</summary>
        Greater,
        /// <summary>Greater than or equal to.</summary>
        GreaterOrEqual,
        /// <summary>Exact equality.</summary>
        Equal
    }

    /// <summary>
    /// A single version comparison constraint.
    /// </summary>
    public readonly struct Constraint
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Constraint"/> struct.
        /// </summary>
        /// <param name="comparator">Comparator to apply.</param>
        /// <param name="version">Version to compare with.</param>
        public Constraint(Comparator comparator, Version version)
        {
            Comparator = comparator;
            Version = version ?? throw new ArgumentNullException(nameof(version));
        }

        /// <summary>
        /// Gets the comparator.
        /// </summary>
        public Comparator Comparator { get; }

        /// <summary>
        /// Gets the comparison version.
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// Evaluates whether the specified version satisfies this constraint.
        /// </summary>
        public bool IsMatch(Version v)
        {
            var cmp = v.CompareTo(Version);
            return Comparator switch
            {
                Comparator.Less => cmp < 0,
                Comparator.LessOrEqual => cmp <= 0,
                Comparator.Greater => cmp > 0,
                Comparator.GreaterOrEqual => cmp >= 0,
                Comparator.Equal => cmp == 0,
                _ => false
            };
        }
    }
}
