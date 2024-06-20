/*
 *  Auto git chenge-log generator.
 * 
 *  Usage:
 *  Parameter 1: Repository path
 *  Parameter 2: Start recording log time, generally the time when the last tag was created on the same branch
 *  Parameter 3 (optional): End recording log time, generally the time when the CI is triggered, default value is Now
 *
 *  Format for time points: year-month-day-hour-minute-second
 *
 *  Example: dotnet AutoChangelog.dll C:/Programs/Project 2024-5-29-8-15-0
*/

using LibGit2Sharp;
using System.Text.RegularExpressions;

namespace AutoChangeLog
{
    class Program
    {
        static string gitRepoPath = "";

        static List<string> types = new List<string> { "feat", "fix", "perf" };
        
        static List<string> scopes = new List<string> { "runtime", "editor", "sample", "cppast", "export", "toolchain", "debugging", "dotnet", "workflow" };
        
        static Dictionary<string, string> typeFullNames = new Dictionary<string, string>() {
            { "feat", "Features"}, { "fix", "Bug Fixes" }, {"perf", "Performance Improvements"}
        };

        static void Main(string[] args)
        {
            // Parse args
            gitRepoPath = Path.GetFullPath(args[0]);  // @"C:/Programs/Project";
            DateTime since = TimeArgToDateTime(args[1]);  // 2024-5-29-8-15-0
            DateTime until;
            if (args.Length > 2)
                until = TimeArgToDateTime(args[2]);
            else
                until = DateTime.Now;

            // collect all commits from git
            var repo = new Repository(gitRepoPath);
            var sinceOffset = new DateTimeOffset(since);
            var thenOffset = new DateTimeOffset(until);
            List<Commit> commitsRaw = new List<Commit>();
            foreach (var c in repo.Commits)
            {
                if (c.Author.When >= sinceOffset && c.Author.When <= thenOffset)
                {
                    commitsRaw.Add(c);
                }
            }

            // filter
            List<CommitMessage> commits = Filter(commitsRaw);

            // convert commit messages to text
            string text = Convert(commits);

            // ouput text to CHANGELOG.md
            string changelogPath = Path.Combine(Program.gitRepoPath, "Docs", "CHANGELOG.md");  //@"C:/Programs/Project/Docs/CHANGELOG.md";

            string appendedText = $"#  {until.Year}-{until.Month}-{until.Day}\n" + text;
            string originalText = File.ReadAllText(changelogPath);

            using (StreamWriter sw = new StreamWriter(changelogPath, false))
            {
                sw.Write(appendedText);
                sw.Write(originalText);
                sw.Close();
            }
        }

        /// Param 'arg' Format: 2024-5-29-8-15-0
        static DateTime TimeArgToDateTime(string arg)
        {
            string[] string_arr = arg.Split('-');
            int[] int_arr = new int[6];
            for(int i = 0; i < 6; i++)
            {
                int_arr[i] = int.Parse(string_arr[i]);
            }
            return new DateTime(int_arr[0], int_arr[1], int_arr[2], int_arr[3], int_arr[4], int_arr[5]);
        }

        public struct CommitMessage
        {
            public string type;
            public string scope;
            public string message;
            public string sha;
            public string shortSha;
            public DateTimeOffset when;
        }

        public static List<CommitMessage> Filter(List<Commit> commitsRaw)
        {
            // 1. simplify commit information
            Dictionary<string, CommitMessage> commits = new Dictionary<string, CommitMessage>();
            foreach (var c in commitsRaw)
            {
                if (ParseCommitMessage(c.Message, out string type, out string scope, out string message) &&
                    types.Contains(type))
                {
                    string shortSha = c.Sha.Substring(0, 8);
                    commits.Add(shortSha, new CommitMessage
                    {
                        type = type,
                        scope = scope,
                        message = message,
                        sha = c.Sha,
                        shortSha = shortSha,
                        when = c.Author.When,
                    });
                }
            }
            // 2. modify or delete commits, based on the `changelog-modify.txt`
            string modifyListPath = Path.GetFullPath(Path.Combine(gitRepoPath, "commitlint.config.js"));
            Dictionary<string, string> modifyList = ReadModifyList(modifyListPath);
            List<string> shortShaList = commits.Keys.ToList();
            foreach (string shortSha in shortShaList)
            {
                bool found = modifyList.TryGetValue(shortSha, out string? modifyMessage);
                if (!found)
                    continue;
                modifyMessage = modifyMessage?.Trim();

                // delete
                if (string.IsNullOrEmpty(modifyMessage))
                {
                    commits.Remove(shortSha);
                    continue;
                }

                // modify
                if (ParseCommitMessage(modifyMessage, out string type, out string scope, out string message))
                {
                    CommitMessage commitToBeModified = commits[shortSha];
                    commitToBeModified.type = type;
                    commitToBeModified.scope = scope;
                    commitToBeModified.message = message;
                    commits[shortSha] = commitToBeModified;
                    continue;
                }
            }

            // 3. classify commits by type, scope and time
            List<CommitMessage> retList = commits.Values.ToList();
            retList.Sort(Comparison);
            return retList;
        }

        public static bool ParseCommitMessage(string fullMessage, out string type, out string scope, out string message)
        {
            type = "";
            scope = "";
            message = "";

            if (!Regex.IsMatch(fullMessage, @"\w+\(\w+\):.*"))
            {
                return false;
            }

            var match = Regex.Match(fullMessage, @"(\w+)\((\w+)\):\s?(.*)");
            type = match.Groups[1].Value;
            scope = match.Groups[2].Value;
            message = match.Groups[3].Value.Trim();
            return true;
        }
         static Dictionary<string, string> ReadModifyList(string modifyListPath)
        {
            Dictionary<string, string> modifyList = new Dictionary<string, string>();
            string content = File.ReadAllText(modifyListPath);

            string changelogModifyPattern = @"changelog_modify\s*=\s*{([^}]+)}";
            Match match = Regex.Match(content, changelogModifyPattern);
            if (match.Success)
            {
                string keyValuePairs = match.Groups[1].Value;
                content = content.Replace(keyValuePairs, "");

                string keyValuePattern = @"'([^']+)'\s*:\s*'([^']*)'";
                MatchCollection keyValueMatches = Regex.Matches(keyValuePairs, keyValuePattern);
                foreach (Match keyValueMatch in keyValueMatches)
                {
                    string key = keyValueMatch.Groups[1].Value;
                    string value = keyValueMatch.Groups[2].Value;
                    modifyList.Add(key, value);
                }
            }
            else
                content = "/* ReadModidyList failded. */" + content;

            File.WriteAllText(modifyListPath, content);
            return modifyList;
        }

        public static int Comparison(CommitMessage a, CommitMessage b)
        {
            if (types.IndexOf(a.type) < types.IndexOf(b.type))
            {
                return -1;
            }
            if (types.IndexOf(a.type) > types.IndexOf(b.type))
            {
                return 1;
            }
            if (scopes.IndexOf(a.scope) < scopes.IndexOf(b.scope))
            {
                return -1;
            }
            if (scopes.IndexOf(a.scope) > scopes.IndexOf(b.scope))
            {
                return 1;
            }
            // reverse time order
            if (a.when > b.when)
            {
                return -1;
            }
            if (a.when < b.when)
            {
                return 1;
            }
            return a.message.CompareTo(b.message);
        }

        // convert commits to text
        public static string Convert(List<CommitMessage> commits)
        {
            string text = "";
            string lastType = "";
            string lastMessage = "";
            foreach (var c in commits)
            {
                if (lastType != c.type)
                {
                    text += "\n\n### " + typeFullNames[c.type] + "\n\n";
                    lastType = c.type;
                }
                if (lastMessage == c.message)
                {
                    text = text.Insert(text.Length - 2, $", [{c.shortSha}](http://your-link)");
                }
                else
                {
                    text += $"* **{c.scope}:** {c.message} ([{c.shortSha}](http://your-link))\n";
                    lastMessage = c.message;
                }
            }
            return text + "\n\n";
        }
    }
}