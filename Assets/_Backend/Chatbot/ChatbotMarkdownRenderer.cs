using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;

namespace HABIMON.Chatbot
{
    /// <summary>
    /// Converts assistant Markdown to a small, controlled subset of TextMeshPro rich text.
    /// Raw HTML is disabled in Markdig; source text is HTML-decoded and escaped before it is
    /// written into TMP markup. During streaming unmatched inline markers are withheld until
    /// their matching delimiter arrives.
    /// </summary>
    public static class ChatbotMarkdownRenderer
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .DisableHtml()
            .Build();

        private static readonly Regex HtmlTag = new Regex(
            @"<(?<close>/)?(?<name>[A-Za-z][A-Za-z0-9]*)(?<attrs>\s[^<>]*?)?\s*/?>",
            RegexOptions.Compiled);
        private static readonly Regex OrderedListStart = new Regex(
            @"(?:^|\s)start\s*=\s*['""]?(?<value>[0-9]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ImageAlt = new Regex(
            @"(?:^|\s)alt\s*=\s*['""](?<value>.*?)['""]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex IncompleteInlineLink = new Regex(
            @"(?<image>!)?\[(?<label>[^\[\]\r\n]*)\]\([^()\r\n]*$", RegexOptions.Compiled);
        private static readonly Regex IncompleteLinkLabel = new Regex(
            @"(?<image>!)?\[(?<label>[^\[\]\r\n]*)$", RegexOptions.Compiled);
        private static readonly Regex IncompleteBlockMarker = new Regex(
            @"(?m)^(?<indent> {0,3})(?:#{1,6}|>|[-+*]|[0-9]{1,9}[.)])[ \t]*$", RegexOptions.Compiled);

        private struct ListState
        {
            public bool Ordered;
            public int NextNumber;
        }

        private struct Removal
        {
            public int Start;
            public int Length;

            public Removal(int start, int length)
            {
                Start = start;
                Length = length;
            }
        }

        /// <summary>
        /// Returns TMP-safe display markup. Pass isFinal=false while deltas are arriving and
        /// isFinal=true for persisted history or a terminal stream result.
        /// </summary>
        public static string RenderToTmp(string markdown, bool isFinal)
        {
            if (string.IsNullOrEmpty(markdown)) return string.Empty;

            // Normalize decomposed accent sequences before TMP lays out the text. Vietnamese
            // combines tone and vowel marks frequently, and NFC gives them stable precomposed glyphs.
            string displayMarkdown = markdown.Normalize(NormalizationForm.FormC);
            if (!isFinal)
            {
                displayMarkdown = HideUnmatchedInlineMarkers(displayMarkdown);
                displayMarkdown = HideIncompleteLinkSyntax(displayMarkdown);
                displayMarkdown = IncompleteBlockMarker.Replace(displayMarkdown, string.Empty);
            }
            string html = Markdig.Markdown.ToHtml(displayMarkdown, Pipeline);
            return ConvertHtmlToTmp(html);
        }

        private static string HideIncompleteLinkSyntax(string markdown)
        {
            Match link = IncompleteInlineLink.Match(markdown);
            if (link.Success)
            {
                string label = link.Groups["label"].Value;
                string visible = link.Groups["image"].Success ? "[" + label + "]" : label;
                return markdown.Substring(0, link.Index) + visible;
            }

            Match labelOnly = IncompleteLinkLabel.Match(markdown);
            if (labelOnly.Success)
            {
                string label = labelOnly.Groups["label"].Value;
                string visible = labelOnly.Groups["image"].Success ? "[" + label + "]" : label;
                return markdown.Substring(0, labelOnly.Index) + visible;
            }

            return markdown;
        }

        private static string HideUnmatchedInlineMarkers(string markdown)
        {
            var removals = new List<Removal>();
            var openDelimiters = new Dictionary<string, Stack<int>>(StringComparer.Ordinal);
            var codeRuns = new Dictionary<int, Stack<int>>();
            int activeCodeRun = 0;
            char fenceCharacter = '\0';
            int fenceLength = 0;
            int lineStart = 0;

            while (lineStart <= markdown.Length)
            {
                int lineEnd = markdown.IndexOf('\n', lineStart);
                if (lineEnd < 0) lineEnd = markdown.Length;
                int contentEnd = lineEnd > lineStart && markdown[lineEnd - 1] == '\r' ? lineEnd - 1 : lineEnd;
                int first = lineStart;
                while (first < contentEnd && first - lineStart < 3 && markdown[first] == ' ') first++;
                int markerEnd = first;
                while (markerEnd < contentEnd && (markdown[markerEnd] == '`' || markdown[markerEnd] == '~')) markerEnd++;
                int markerLength = markerEnd - first;
                char marker = markerLength > 0 ? markdown[first] : '\0';
                bool fenceLine = first - lineStart <= 3 && markerLength >= 3 &&
                    (marker == '`' || marker == '~');

                if (fenceCharacter != '\0')
                {
                    if (fenceLine && marker == fenceCharacter && markerLength >= fenceLength)
                    {
                        fenceCharacter = '\0';
                        fenceLength = 0;
                    }
                    else if (lineEnd == markdown.Length && marker == fenceCharacter &&
                        markerLength > 0 && markerLength < fenceLength && markerEnd == contentEnd)
                    {
                        // A closing code fence may be split across SSE chunks. Keep its partial
                        // backticks off screen until the closing run is complete.
                        removals.Add(new Removal(first, markerLength));
                    }

                    if (lineEnd == markdown.Length) break;
                    lineStart = lineEnd + 1;
                    continue;
                }

                if (fenceLine)
                {
                    fenceCharacter = marker;
                    fenceLength = markerLength;
                    if (lineEnd == markdown.Length) break;
                    lineStart = lineEnd + 1;
                    continue;
                }

                int index = lineStart;
                while (index < contentEnd)
                {
                    char current = markdown[index];
                    if (current == '\\' && index + 1 < contentEnd)
                    {
                        index += 2;
                        continue;
                    }

                    if (current == '`')
                    {
                        int run = CountRun(markdown, index, contentEnd, '`');
                        if (activeCodeRun == 0)
                        {
                            if (!codeRuns.TryGetValue(run, out Stack<int> codeStack))
                            {
                                codeStack = new Stack<int>();
                                codeRuns.Add(run, codeStack);
                            }
                            codeStack.Push(index);
                            activeCodeRun = run;
                        }
                        else if (run == activeCodeRun && codeRuns.TryGetValue(run, out Stack<int> closingCodeStack) && closingCodeStack.Count > 0)
                        {
                            closingCodeStack.Pop();
                            activeCodeRun = 0;
                        }
                        index += run;
                        continue;
                    }

                    if (activeCodeRun > 0)
                    {
                        index++;
                        continue;
                    }

                    if (current == '*' || current == '_' || current == '~')
                    {
                        int run = CountRun(markdown, index, contentEnd, current);
                        string delimiter = DelimiterFor(current, run);
                        if (delimiter != null)
                        {
                            bool canOpen = CanOpen(markdown, index, delimiter.Length, current);
                            bool canClose = CanClose(markdown, index, delimiter.Length, current, contentEnd);
                            if (canClose && openDelimiters.TryGetValue(delimiter, out Stack<int> closingStack) && closingStack.Count > 0)
                            {
                                closingStack.Pop();
                            }
                            else if (canOpen)
                            {
                                if (!openDelimiters.TryGetValue(delimiter, out Stack<int> openingStack))
                                {
                                    openingStack = new Stack<int>();
                                    openDelimiters.Add(delimiter, openingStack);
                                }
                                openingStack.Push(index);
                            }
                            else if (lineEnd == markdown.Length && index + delimiter.Length == contentEnd)
                            {
                                removals.Add(new Removal(index, delimiter.Length));
                            }
                            index += delimiter.Length;
                            continue;
                        }
                        index += run;
                        continue;
                    }

                    index++;
                }

                if (lineEnd == markdown.Length) break;
                lineStart = lineEnd + 1;
            }

            foreach (Stack<int> stack in openDelimiters.Values)
                while (stack.Count > 0) removals.Add(new Removal(stack.Pop(), 1));
            foreach (KeyValuePair<int, Stack<int>> entry in codeRuns)
                while (entry.Value.Count > 0) removals.Add(new Removal(entry.Value.Pop(), entry.Key));

            if (removals.Count == 0) return markdown;
            removals.Sort((left, right) => right.Start.CompareTo(left.Start));
            var result = new StringBuilder(markdown);
            int lastStart = int.MaxValue;
            foreach (Removal removal in removals)
            {
                // An unmatched run is removed as one token. Delimiter stacks store its first
                // character, so consume the full token length here.
                if (removal.Start >= lastStart) continue;
                int length = removal.Length;
                if (length == 1 && removal.Start < markdown.Length)
                {
                    char marker = markdown[removal.Start];
                    if (marker == '*' || marker == '_' || marker == '~')
                        length = CountRun(markdown, removal.Start, markdown.Length, marker);
                }
                result.Remove(removal.Start, Math.Min(length, result.Length - removal.Start));
                lastStart = removal.Start;
            }
            return result.ToString();
        }

        private static string DelimiterFor(char marker, int run)
        {
            if (marker == '~') return run >= 2 ? "~~" : null;
            if (run >= 3) return marker == '*' ? "***" : "___";
            if (run >= 2) return marker == '*' ? "**" : "__";
            return marker == '*' || marker == '_' ? marker.ToString() : null;
        }

        private static bool CanOpen(string text, int index, int length, char marker)
        {
            int next = index + length;
            if (next >= text.Length || char.IsWhiteSpace(text[next])) return false;
            if (marker == '_' && index > 0 && char.IsLetterOrDigit(text[index - 1]) && char.IsLetterOrDigit(text[next]))
                return false;
            return true;
        }

        private static bool CanClose(string text, int index, int length, char marker, int lineEnd)
        {
            if (index <= 0 || char.IsWhiteSpace(text[index - 1])) return false;
            int next = index + length;
            if (marker == '_' && next < lineEnd && char.IsLetterOrDigit(text[index - 1]) && char.IsLetterOrDigit(text[next]))
                return false;
            return true;
        }

        private static int CountRun(string value, int start, int end, char character)
        {
            int index = start;
            while (index < end && value[index] == character) index++;
            return index - start;
        }

        private static string ConvertHtmlToTmp(string html)
        {
            var output = new StringBuilder(html.Length + 64);
            var lists = new Stack<ListState>();
            int cursor = 0;
            int quoteDepth = 0;
            int listItemDepth = 0;
            int preDepth = 0;
            int inlineCodeDepth = 0;
            bool inTableRow = false;
            int tableCells = 0;

            foreach (Match match in HtmlTag.Matches(html))
            {
                AppendHtmlText(output, html.Substring(cursor, match.Index - cursor), quoteDepth,
                    preDepth > 0 || inlineCodeDepth > 0);
                cursor = match.Index + match.Length;
                string name = match.Groups["name"].Value.ToLowerInvariant();
                string attributes = match.Groups["attrs"].Value;
                bool closing = match.Groups["close"].Success;
                if (!closing)
                {
                    switch (name)
                    {
                        case "p":
                            if (listItemDepth == 0 && quoteDepth == 0) EnsureBlockStart(output);
                            break;
                        case "h1": case "h2": case "h3": case "h4": case "h5": case "h6":
                            EnsureBlockStart(output);
                            output.Append("<size=").Append(HeadingPercent(name)).Append("%><b>");
                            break;
                        case "strong": case "b": output.Append("<b>"); break;
                        case "em": case "i": output.Append("<i>"); break;
                        case "del": case "s": output.Append("<s>"); break;
                        case "code":
                            if (preDepth == 0)
                            {
                                inlineCodeDepth++;
                                output.Append("<mark=#EAF2F6>");
                            }
                            break;
                        case "pre":
                            EnsureBlockStart(output);
                            preDepth++;
                            output.Append("<mark=#F1F5F8>");
                            break;
                        case "blockquote":
                            EnsureBlockStart(output);
                            quoteDepth++;
                            output.Append("<i>");
                            AppendQuotePrefix(output, quoteDepth);
                            break;
                        case "ul": case "ol":
                            if (listItemDepth == 0) EnsureBlockStart(output);
                            lists.Push(new ListState
                            {
                                Ordered = name == "ol",
                                NextNumber = ParseListStart(attributes)
                            });
                            break;
                        case "li":
                            EnsureLineStart(output);
                            AppendQuotePrefix(output, quoteDepth);
                            if (lists.Count > 0)
                            {
                                ListState state = lists.Pop();
                                for (int i = 1; i < lists.Count; i++) output.Append("  ");
                                if (state.Ordered) output.Append(state.NextNumber++).Append(". ");
                                else output.Append("• ");
                                lists.Push(state);
                            }
                            listItemDepth++;
                            break;
                        case "br": output.Append('\n'); break;
                        case "hr": EnsureBlockStart(output); output.Append("────────────"); AppendLineBreak(output, 2); break;
                        case "a": output.Append("<u><color=#176B9A>"); break;
                        case "img":
                            Match alt = ImageAlt.Match(attributes);
                            if (alt.Success) AppendPlainText(output, "[" + WebUtility.HtmlDecode(alt.Groups["value"].Value) + "]", quoteDepth);
                            break;
                        case "table": EnsureBlockStart(output); break;
                        case "tr":
                            EnsureLineStart(output);
                            inTableRow = true;
                            tableCells = 0;
                            break;
                        case "td": case "th":
                            if (inTableRow && tableCells > 0) output.Append("  |  ");
                            tableCells++;
                            break;
                        case "input":
                            output.Append(attributes.IndexOf("checked", StringComparison.OrdinalIgnoreCase) >= 0 ? "☑ " : "☐ ");
                            break;
                        case "sup": output.Append("<sup>"); break;
                        case "sub": output.Append("<sub>"); break;
                        case "kbd": output.Append("<mark=#EAF2F6>"); break;
                    }

                    continue;
                }

                switch (name)
                {
                    case "p": if (listItemDepth == 0) AppendLineBreak(output, quoteDepth > 0 ? 1 : 2); break;
                    case "h1": case "h2": case "h3": case "h4": case "h5": case "h6":
                        output.Append("</b></size>"); AppendLineBreak(output, quoteDepth > 0 ? 1 : 2); break;
                    case "strong": case "b": output.Append("</b>"); break;
                    case "em": case "i": output.Append("</i>"); break;
                    case "del": case "s": output.Append("</s>"); break;
                    case "code":
                        if (preDepth == 0)
                        {
                            output.Append("</mark>");
                            inlineCodeDepth = Math.Max(0, inlineCodeDepth - 1);
                        }
                        break;
                    case "pre": output.Append("</mark>"); preDepth = Math.Max(0, preDepth - 1); AppendLineBreak(output, 2); break;
                    case "blockquote": output.Append("</i>"); quoteDepth = Math.Max(0, quoteDepth - 1); AppendLineBreak(output, 2); break;
                    case "ul": case "ol": if (lists.Count > 0) lists.Pop(); if (listItemDepth == 0) AppendLineBreak(output, 1); break;
                    case "li": listItemDepth = Math.Max(0, listItemDepth - 1); AppendLineBreak(output, 1); break;
                    case "a": output.Append("</color></u>"); break;
                    case "tr":
                        if (inTableRow) AppendLineBreak(output, 1);
                        inTableRow = false;
                        break;
                    case "table": AppendLineBreak(output, 2); break;
                    case "sup": output.Append("</sup>"); break;
                    case "sub": output.Append("</sub>"); break;
                    case "kbd": output.Append("</mark>"); break;
                }
            }

            AppendHtmlText(output, html.Substring(cursor), quoteDepth,
                preDepth > 0 || inlineCodeDepth > 0);
            return NormalizeLineBreaks(output.ToString());
        }

        private static int ParseListStart(string attributes)
        {
            Match start = OrderedListStart.Match(attributes);
            return start.Success && int.TryParse(start.Groups["value"].Value, out int value) ? value : 1;
        }

        private static int HeadingPercent(string tag)
        {
            switch (tag)
            {
                case "h1": return 145;
                case "h2": return 135;
                case "h3": return 125;
                default: return 115;
            }
        }

        private static void AppendHtmlText(StringBuilder output, string html, int quoteDepth, bool preserveWhitespace)
        {
            if (string.IsNullOrEmpty(html)) return;
            string decoded = WebUtility.HtmlDecode(html);
            if (!preserveWhitespace && string.IsNullOrWhiteSpace(decoded)) return;
            AppendPlainText(output, decoded, quoteDepth);
        }

        private static void AppendPlainText(StringBuilder output, string value, int quoteDepth)
        {
            if (string.IsNullOrEmpty(value)) return;
            // LiberationSans SDF has no glyphs for the two emojis observed in Habi's
            // replies; use readable display alternatives while preserving server Markdown.
            value = value.Replace("\U0001F389", "Chúc mừng!")
                .Replace("\U0001F3B5", "♪");
            bool needsQuotePrefix = output.Length == 0 || output[output.Length - 1] == '\n';
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (character == '\r') continue;
                if (needsQuotePrefix && character != '\n' && quoteDepth > 0)
                    AppendQuotePrefix(output, quoteDepth);
                if (character == '&') output.Append("&amp;");
                else if (character == '<') output.Append("&lt;");
                else if (character == '>') output.Append("&gt;");
                else output.Append(character);
                needsQuotePrefix = character == '\n';
            }
        }

        private static void AppendQuotePrefix(StringBuilder output, int depth)
        {
            for (int i = 0; i < depth; i++) output.Append("▏ ");
        }

        private static void EnsureBlockStart(StringBuilder output)
        {
            if (output.Length == 0) return;
            AppendLineBreak(output, 2);
        }

        private static void EnsureLineStart(StringBuilder output)
        {
            if (output.Length > 0 && output[output.Length - 1] != '\n') output.Append('\n');
        }

        private static void AppendLineBreak(StringBuilder output, int count)
        {
            while (output.Length > 0 && output[output.Length - 1] == ' ') output.Length--;
            int existing = 0;
            for (int i = output.Length - 1; i >= 0 && output[i] == '\n'; i--) existing++;
            while (existing++ < count) output.Append('\n');
        }

        private static string NormalizeLineBreaks(string value)
        {
            var output = new StringBuilder(value.Length);
            int lineBreaks = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (character == '\r') continue;
                if (character == '\n')
                {
                    if (lineBreaks < 2) output.Append('\n');
                    lineBreaks++;
                }
                else
                {
                    output.Append(character);
                    lineBreaks = 0;
                }
            }
            return output.ToString().Trim('\n');
        }
    }
}
