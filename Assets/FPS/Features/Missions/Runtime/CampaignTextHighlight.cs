using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FPS
{
    /// <summary>
    /// Formats campaign HUD, radio, and journal text with dynamic SelectedUiTheme colors
    /// so key clues, targets, hazards, and action directives stand out immediately.
    /// </summary>
    public static class CampaignTextHighlight
    {
        private const int MaxHighlightsPerLine = 3;

        private readonly struct KeywordRule
        {
            public readonly string Pattern;
            public readonly bool Danger;
            public readonly Regex Regex;

            public KeywordRule(string literal, bool danger)
            {
                Pattern = literal;
                Danger = danger;
                string escaped = Regex.Escape(literal);
                bool startsWord = char.IsLetterOrDigit(literal[0]);
                bool endsWord = char.IsLetterOrDigit(literal[literal.Length - 1]);
                string prefix = startsWord ? @"(?<![A-Za-z0-9_])" : string.Empty;
                string suffix = endsWord ? @"(?![A-Za-z0-9_])" : string.Empty;
                Regex = new Regex(prefix + escaped + suffix, RegexOptions.Compiled | RegexOptions.CultureInvariant);
            }
        }

        private static readonly Regex RichTextTagRegex = new Regex(@"<color=#[0-9A-Fa-f]{6,8}>|</color>|<b>|</b>", RegexOptions.Compiled);
        private static readonly Regex ExistingColorSpanRegex = new Regex(@"<color=#[0-9A-Fa-f]{6,8}>.*?</color>", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex MarkdownBoldRegex = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled | RegexOptions.Singleline);

        // Ordered longest-first so compound clues (e.g., "AL-04 / K6") match before individual tokens.
        private static readonly KeywordRule[] KeywordRules = BuildRules();

        private static KeywordRule[] BuildRules()
        {
            var entries = new (string text, bool danger)[]
            {
                // Danger / exclusion terms (Theme.Danger)
                ("TEST RIG (4) OFF", true),
                ("TEST RIG OFF", true),
                ("TEST RIG (4)", true),
                ("Kill any Tank", true),
                ("ignore that order", true),
                ("green alarm lamp", true),
                ("shutters closed", true),
                ("contradiction", true),
                ("empty drawer", true),
                ("refuse receipt", true),
                ("sample-only", true),
                ("I'm down!", true),
                ("DECEASED", true),
                ("deceased", true),
                ("Infector", true),
                ("Screamer", true),
                ("Overload", true),
                ("scrubbed", true),
                ("LOCKED", true),
                ("03:05", true),
                ("Tank", true),
                ("T-9", true),

                // Accent / clues / directives / targets (Theme.Accent)
                ("E-02 / T9-17 / C12 / P046", false),
                ("SAFETY (2) + DATA (2) + FREIGHT LIFT (2) = 6", false),
                ("Factory Utilities Procedure", false),
                ("Emergency Power Allocation", false),
                ("NORTHBRIDGE / 02:40", false),
                ("P046 / C12 / 02:40", false),
                ("BACKUP COLD STORE", false),
                ("maintenance storage", false),
                ("upper records room", false),
                ("patient identities", false),
                ("Delivery Manifest", false),
                ("B2 Archive Index", false),
                ("preparation zone", false),
                ("Dr. Adrian Cole", false),
                ("B2 Access Card", false),
                ("Evade the Tank", false),
                ("physical lever", false),
                ("boarding zone", false),
                ("security desk", false),
                ("FREIGHT LIFT (2)", false),
                ("FREIGHT LIFT", false),
                ("Service Fuse", false),
                ("west breaker", false),
                ("east breaker", false),
                ("freight lift", false),
                ("Northbridge", false),
                ("NORTHBRIDGE", false),
                ("Marcus Hale", false),
                ("AL-04 / K6", false),
                ("service exit", false),
                ("refrigerated", false),
                ("PRODUCTION", false),
                ("Group C12", false),
                ("SAFETY (2)", false),
                ("halt order", false),
                ("full copy", false),
                ("Maya Reed", false),
                ("Vanguard", false),
                ("Level B2", false),
                ("DATA (2)", false),
                ("6 units", false),
                ("T9-17", false),
                ("AL-04", false),
                ("02:40", false),
                ("03:50", false),
                ("ALIVE", false),
                ("P046", false),
                ("E-02", false),
                ("BRT", false),
                ("C12", false),
                ("[F]", false),
                ("[J]", false),
                ("[V]", false),
                ("K6", false)
            };

            Array.Sort(entries, (a, b) => b.text.Length.CompareTo(a.text.Length));
            var rules = new KeywordRule[entries.Length];
            for (int i = 0; i < entries.Length; i++)
                rules[i] = new KeywordRule(entries[i].text, entries[i].danger);
            return rules;
        }

        private static bool IsDangerToken(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return false;

            string trimmed = content.Trim();
            for (int i = 0; i < KeywordRules.Length; i++)
            {
                if (!KeywordRules[i].Danger)
                    continue;

                if (string.Equals(trimmed, KeywordRules[i].Pattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return trimmed.IndexOf("DECEASED", StringComparison.OrdinalIgnoreCase) >= 0
                || trimmed.IndexOf("03:05", StringComparison.OrdinalIgnoreCase) >= 0
                || trimmed.IndexOf("TEST RIG", StringComparison.OrdinalIgnoreCase) >= 0
                || trimmed.IndexOf("LOCKED", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(trimmed, "T-9", StringComparison.OrdinalIgnoreCase);
        }

        public static void ResolveHexColors(SelectedUiTheme theme, bool onPaper, out string accentHex, out string dangerHex)
        {
            Color accent = theme != null ? theme.Accent : new Color32(233, 166, 76, 255);
            Color danger = theme != null ? theme.Danger : new Color32(216, 75, 75, 255);
            if (onPaper)
            {
                Color bg = theme != null ? theme.Background : new Color32(23, 24, 24, 255);
                accent = Color.Lerp(accent, bg, 0.42f);
                danger = Color.Lerp(danger, bg, 0.20f);
            }
            accentHex = ColorUtility.ToHtmlStringRGB(accent);
            dangerHex = ColorUtility.ToHtmlStringRGB(danger);
        }

        public static string Format(string text, SelectedUiTheme theme = null, bool onPaper = false, bool markdownOnly = false)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? string.Empty;

            if (theme == null)
                theme = SelectedUiTheme.Current;

            ResolveHexColors(theme, onPaper, out string accentHex, out string dangerHex);

            string processed = MarkdownBoldRegex.Replace(text, match =>
            {
                string inner = match.Groups[1].Value;
                string hex = IsDangerToken(inner) ? dangerHex : accentHex;
                return $"<color=#{hex}>{inner}</color>";
            });

            if (markdownOnly)
                return processed;

            string[] lines = processed.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                lines[i] = HighlightLine(lines[i], accentHex, dangerHex);

            return string.Join("\n", lines);
        }

        public static string FormatFileRecord(CampaignFileDefinition file, SelectedUiTheme theme = null, bool onPaper = false)
        {
            if (file == null)
                return string.Empty;

            string summary = file.summary ?? string.Empty;
            string body = file.body ?? string.Empty;
            bool hasSummary = !string.IsNullOrWhiteSpace(summary)
                && !string.Equals(summary.Trim(), body.Trim(), StringComparison.Ordinal);

            if (hasSummary)
            {
                string formattedSummary = Format(summary, theme, onPaper, markdownOnly: summary.Contains("**"));
                string formattedBody = string.IsNullOrWhiteSpace(body)
                    ? string.Empty
                    : Format(body, theme, onPaper, markdownOnly: body.Contains("**"));
                return string.IsNullOrEmpty(formattedBody)
                    ? "KEY DATA\n" + formattedSummary
                    : "KEY DATA\n" + formattedSummary + "\n\nFULL RECORD\n" + formattedBody;
            }

            string raw = string.IsNullOrWhiteSpace(body) ? summary : body;
            return Format(raw, theme, onPaper, markdownOnly: raw.Contains("**"));
        }

        public static string StripRichText(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : RichTextTagRegex.Replace(text, string.Empty);
        }

        private static string HighlightLine(string line, string accentHex, string dangerHex)
        {
            if (string.IsNullOrEmpty(line))
                return line;

            var matches = new List<(int start, int length, string hex)>();
            var occupied = new List<(int start, int end)>();

            // Reserve already-colored spans so HighlightLine is idempotent.
            foreach (Match existing in ExistingColorSpanRegex.Matches(line))
                occupied.Add((existing.Index, existing.Index + existing.Length));

            for (int r = 0; r < KeywordRules.Length && matches.Count < MaxHighlightsPerLine; r++)
            {
                ref readonly KeywordRule rule = ref KeywordRules[r];
                foreach (Match m in rule.Regex.Matches(line))
                {
                    int start = m.Index;
                    int end = start + m.Length;
                    if (Overlaps(occupied, start, end))
                        continue;

                    matches.Add((start, m.Length, rule.Danger ? dangerHex : accentHex));
                    occupied.Add((start, end));
                    if (matches.Count >= MaxHighlightsPerLine)
                        break;
                }
            }

            if (matches.Count == 0)
                return line;

            matches.Sort((a, b) => a.start.CompareTo(b.start));
            var sb = new StringBuilder(line.Length + matches.Count * 24);
            int cursor = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                var item = matches[i];
                if (item.start > cursor)
                    sb.Append(line, cursor, item.start - cursor);
                sb.Append("<color=#").Append(item.hex).Append('>');
                sb.Append(line, item.start, item.length);
                sb.Append("</color>");
                cursor = item.start + item.length;
            }
            if (cursor < line.Length)
                sb.Append(line, cursor, line.Length - cursor);

            return sb.ToString();
        }

        private static bool Overlaps(List<(int start, int end)> occupied, int start, int end)
        {
            for (int i = 0; i < occupied.Count; i++)
            {
                if (start < occupied[i].end && end > occupied[i].start)
                    return true;
            }
            return false;
        }
    }
}
