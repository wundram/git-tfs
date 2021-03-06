using System;
using System.IO;
using Sep.Git.Tfs.Commands;
using Sep.Git.Tfs.Core;
using System.Text.RegularExpressions;
using StructureMap;

namespace Sep.Git.Tfs.Util
{
    /// <summary>
    /// Creates a new <see cref="CheckinOptions"/> that is customized based 
    /// on extracting special git-tfs commands from a git commit message.
    /// </summary>
    /// <remarks>
    /// This class handles the pre-checkin commit message parsing that
    /// enables special git-tfs commands: 
    /// https://github.com/git-tfs/git-tfs/wiki/Special-actions-in-commit-messages
    /// </remarks>
    public class CommitSpecificCheckinOptionsFactory
    {
        private readonly TextWriter writer;
        private readonly Globals globals;

        public CommitSpecificCheckinOptionsFactory(TextWriter writer, Globals globals)
        {
            this.writer = writer;
            this.globals = globals;
        }

        public CheckinOptions BuildCommitSpecificCheckinOptions(CheckinOptions sourceCheckinOptions, string commitMessage)
        {
            var customCheckinOptions = Clone(sourceCheckinOptions);

            customCheckinOptions.CheckinComment = commitMessage;

            ProcessWorkItemCommands(customCheckinOptions, writer);

            ProcessCheckinNoteCommands(customCheckinOptions, writer);

            ProcessForceCommand(customCheckinOptions, writer);

            return customCheckinOptions;
        }

        public CheckinOptions BuildCommitSpecificCheckinOptions(CheckinOptions sourceCheckinOptions, string commitMessage, GitCommit commit)
        {
            var customCheckinOptions = Clone(sourceCheckinOptions);

            customCheckinOptions.CheckinComment = commitMessage;

            ProcessWorkItemCommands(customCheckinOptions, writer);

            ProcessCheckinNoteCommands(customCheckinOptions, writer);

            ProcessForceCommand(customCheckinOptions, writer);

            ProcessAuthor(customCheckinOptions, writer, commit);

            return customCheckinOptions;
        }

        private CheckinOptions Clone(CheckinOptions source)
        {
            CheckinOptions clone = new CheckinOptions();

            clone.CheckinComment = source.CheckinComment;
            clone.NoGenerateCheckinComment = source.NoGenerateCheckinComment;
            clone.NoMerge = source.NoMerge;
            clone.OverrideReason = source.OverrideReason;
            clone.Force = source.Force;
            clone.OverrideGatedCheckIn = source.OverrideGatedCheckIn;
            clone.WorkItemsToAssociate.AddRange(source.WorkItemsToAssociate);
            clone.WorkItemsToResolve.AddRange(source.WorkItemsToResolve);
            clone.AuthorsFilePath = source.AuthorsFilePath;
            clone.AuthorTfsUserId = source.AuthorTfsUserId;
            foreach (var note in source.CheckinNotes)
            {
                clone.CheckinNotes[note.Key] = note.Value;
            }

            return clone;
        }

        private void ProcessWorkItemCommands(CheckinOptions checkinOptions, TextWriter writer)
        {
            MatchCollection workitemMatches;
            if ((workitemMatches = GitTfsConstants.TfsWorkItemRegex.Matches(checkinOptions.CheckinComment)).Count > 0)
            {
                foreach (Match match in workitemMatches)
                {
                    switch (match.Groups["action"].Value)
                    {
                        case "associate":
                            writer.WriteLine("Associating with work item {0}", match.Groups["item_id"]);
                            checkinOptions.WorkItemsToAssociate.Add(match.Groups["item_id"].Value);
                            break;
                        case "resolve":
                            writer.WriteLine("Resolving work item {0}", match.Groups["item_id"]);
                            checkinOptions.WorkItemsToResolve.Add(match.Groups["item_id"].Value);
                            break;
                    }
                }
                checkinOptions.CheckinComment = GitTfsConstants.TfsWorkItemRegex.Replace(checkinOptions.CheckinComment, "").Trim(' ', '\r', '\n');
            }
        }

        private void ProcessCheckinNoteCommands(CheckinOptions checkinOptions, TextWriter writer)
        {
            foreach (Match match in GitTfsConstants.TfsReviewerRegex.Matches(checkinOptions.CheckinComment))
            {
                string reviewer = match.Groups["reviewer"].Value;
                if (!string.IsNullOrWhiteSpace(reviewer))
                {
                    switch (match.Groups["type"].Value)
                    {
                        case "code":
                            writer.WriteLine("Code reviewer: {0}", reviewer);
                            checkinOptions.CheckinNotes.Add("Code Reviewer", reviewer);
                            break;
                        case "security":
                            writer.WriteLine("Security reviewer: {0}", reviewer);
                            checkinOptions.CheckinNotes.Add("Security Reviewer", reviewer);
                            break;
                        case "performance":
                            writer.WriteLine("Performance reviewer: {0}", reviewer);
                            checkinOptions.CheckinNotes.Add("Performance Reviewer", reviewer);
                            break;
                    }
                }
            }
            checkinOptions.CheckinComment = GitTfsConstants.TfsReviewerRegex.Replace(checkinOptions.CheckinComment, "").Trim(' ', '\r', '\n');
        }



        private void ProcessForceCommand(CheckinOptions checkinOptions, TextWriter writer)
        {
            MatchCollection workitemMatches;
            if ((workitemMatches = GitTfsConstants.TfsForceRegex.Matches(checkinOptions.CheckinComment)).Count == 1)
            {
                string overrideReason = workitemMatches[0].Groups["reason"].Value;

                if (!string.IsNullOrWhiteSpace(overrideReason))
                {
                    writer.WriteLine("Forcing the checkin: {0}", overrideReason);
                    checkinOptions.Force = true;
                    checkinOptions.OverrideReason = overrideReason;
                }
                checkinOptions.CheckinComment = GitTfsConstants.TfsForceRegex.Replace(checkinOptions.CheckinComment, "").Trim(' ', '\r', '\n');
            }
        }



        private void ProcessAuthor(CheckinOptions checkinOptions, TextWriter writer, GitCommit commit)
        {
            // get authors file FIXME
            AuthorsFile af = new AuthorsFile();
            if (!af.Parse(checkinOptions.AuthorsFilePath, globals.GitDir))
                return;

            Author a = af.FindAuthor(commit.AuthorAndEmail);
            if (a == null)
            {
                checkinOptions.AuthorTfsUserId = null;
                return;
            }

            checkinOptions.AuthorTfsUserId = a.TfsUserId;
            writer.WriteLine("Commit was authored by git user {0} {1} ({2})", a.Name, a.Email, a.TfsUserId);
        }

    }
}
