﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using GitHub.Extensions;
using GitHub.Factories;
using GitHub.InlineReviews.Models;
using GitHub.InlineReviews.Services;
using GitHub.InlineReviews.UnitTests.TestDoubles;
using GitHub.Models;
using GitHub.Primitives;
using GitHub.Services;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using NSubstitute;
using NUnit.Framework;
using System.Reactive.Disposables;

namespace GitHub.InlineReviews.UnitTests.Services
{
    public class PullRequestSessionManagerTests
    {
        const int CurrentBranchPullRequestNumber = 15;
        const int NotCurrentBranchPullRequestNumber = 10;
        const string OwnerCloneUrl = "https://github.com/owner/repo";

        public PullRequestSessionManagerTests()
        {
            Splat.ModeDetector.Current.SetInUnitTestRunner(true);
        }

        public class TheConstructor : PullRequestSessionManagerTests
        {
            [Test]
            public void ReadsPullRequestFromCorrectFork()
            {
                var service = CreatePullRequestService();
                service.GetPullRequestForCurrentBranch(null).ReturnsForAnyArgs(
                    Observable.Return(Tuple.Create("fork", CurrentBranchPullRequestNumber)));

                var connectionManager = CreateConnectionManager();
                var modelFactory = CreateModelServiceFactory();
                var repositoryModel = CreateRepositoryModel();
                var target = new PullRequestSessionManager(
                    service,
                    Substitute.For<IPullRequestSessionService>(),
                    connectionManager,
                    modelFactory,
                    new FakeTeamExplorerServiceHolder(repositoryModel));

                var modelService = modelFactory.CreateBlocking(connectionManager.Connections[0]);
                modelService.Received(1).GetPullRequest("fork", "repo", 15);
            }
        }

        public class TheCurrentSessionProperty : PullRequestSessionManagerTests
        {
            [Test]
            public void CreatesSessionForCurrentBranch()
            {
                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    Substitute.For<IPullRequestSessionService>(),
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));

                Assert.NotNull(target.CurrentSession);
                Assert.True(target.CurrentSession.IsCheckedOut);
            }

            [Test]
            public void CurrentSessionIsNullIfNoPullRequestForCurrentBranch()
            {
                var service = CreatePullRequestService();
                service.GetPullRequestForCurrentBranch(null).ReturnsForAnyArgs(Observable.Empty<Tuple<string, int>>());

                var target = new PullRequestSessionManager(
                    service,
                    Substitute.For<IPullRequestSessionService>(),
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));

                Assert.Null(target.CurrentSession);
            }

            [Test]
            public void CurrentSessionChangesWhenBranchChanges()
            {
                var service = CreatePullRequestService();
                var teService = new FakeTeamExplorerServiceHolder(CreateRepositoryModel());
                var target = new PullRequestSessionManager(
                    service,
                    Substitute.For<IPullRequestSessionService>(),
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    teService);

                var session = target.CurrentSession;

                service.GetPullRequestForCurrentBranch(null).ReturnsForAnyArgs(Observable.Return(Tuple.Create("foo", 22)));
                teService.NotifyActiveRepoChanged();

                Assert.That(session, Is.Not.SameAs(target.CurrentSession));
            }

            [Test]
            public void CurrentSessionChangesWhenRepoChanged()
            {
                var teService = new FakeTeamExplorerServiceHolder(CreateRepositoryModel());
                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    Substitute.For<IPullRequestSessionService>(),
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    teService);

                var session = target.CurrentSession;

                teService.ActiveRepo = CreateRepositoryModel("https://github.com/owner/other");

                Assert.That(session, Is.Not.SameAs(target.CurrentSession));
            }

            [Test]
            public void RepoChangedDoesntCreateNewSessionIfNotNecessary()
            {
                var teService = new FakeTeamExplorerServiceHolder(CreateRepositoryModel());
                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    Substitute.For<IPullRequestSessionService>(),
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    teService);

                var session = target.CurrentSession;

                teService.NotifyActiveRepoChanged();

                Assert.That(session, Is.SameAs(target.CurrentSession));
            }

            [Test]
            public void RepoChangedHandlesNullRepository()
            {
                var teService = new FakeTeamExplorerServiceHolder(CreateRepositoryModel());
                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    Substitute.For<IPullRequestSessionService>(),
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    teService);

                teService.ActiveRepo = null;

                Assert.Null(target.CurrentSession);
            }

            [Test]
            public void CreatesSessionWithCorrectRepositoryOwner()
            {
                var target = new PullRequestSessionManager(
                    CreatePullRequestService("this-owner"),
                    Substitute.For<IPullRequestSessionService>(),
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));

                Assert.AreEqual("this-owner", target.CurrentSession.RepositoryOwner);
            }
        }

        public class TheGetLiveFileMethod : PullRequestSessionManagerTests
        {
            const string FilePath = "test.cs";

            [Test]
            public async Task BaseShaIsSet()
            {
                var textView = CreateTextView();

                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    CreateSessionService(),
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));
                var file = await target.GetLiveFile(FilePath, textView, textView.TextBuffer);

                Assert.That("BASESHA", Is.SameAs(file.BaseSha));
            }

            [Test]
            public async Task CommitShaIsSet()
            {
                var textView = CreateTextView();

                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    CreateSessionService(),
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));
                var file = await target.GetLiveFile(FilePath, textView, textView.TextBuffer);

                Assert.That("TIPSHA", Is.SameAs(file.CommitSha));
            }

            [Test]
            public async Task CommitShaIsNullIfModified()
            {
                var textView = CreateTextView();

                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    CreateSessionService(true),
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));
                var file = await target.GetLiveFile(FilePath, textView, textView.TextBuffer);

                Assert.Null(file.CommitSha);
            }

            [Test]
            public async Task DiffIsSet()
            {
                var textView = CreateTextView();
                var contents = Encoding.UTF8.GetBytes("File contents");
                var diff = new List<DiffChunk>();
                var sessionService = CreateSessionService();

                sessionService.GetContents(textView.TextBuffer).Returns(contents);
                sessionService.GetPullRequestMergeBase(null, null).ReturnsForAnyArgs("MERGE_BASE");
                sessionService.Diff(
                    Arg.Any<ILocalRepositoryModel>(),
                    "MERGE_BASE",
                    "HEADSHA",
                    FilePath,
                    contents).Returns(diff);

                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    sessionService,
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));
                var file = await target.GetLiveFile(FilePath, textView, textView.TextBuffer);

                Assert.That(diff, Is.SameAs(file.Diff));
            }

            [Test]
            public async Task InlineCommentThreadsIsSet()
            {
                var textView = CreateTextView();
                var sessionService = CreateSessionService();
                var threads = new List<IInlineCommentThreadModel>();

                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    sessionService,
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));

                sessionService.BuildCommentThreads(
                    target.CurrentSession.PullRequest,
                    FilePath,
                    Arg.Any<IReadOnlyList<DiffChunk>>())
                    .Returns(threads);

                var file = await target.GetLiveFile(FilePath, textView, textView.TextBuffer);

                Assert.That(threads, Is.SameAs(file.InlineCommentThreads));
            }

            [Test]
            public async Task CreatesTrackingPointsForThreads()
            {
                var textView = CreateTextView();
                var sessionService = CreateSessionService();
                var threads = new List<IInlineCommentThreadModel>
                {
                    CreateInlineCommentThreadModel(1),
                    CreateInlineCommentThreadModel(2),
                };

                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    sessionService,
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));

                sessionService.BuildCommentThreads(
                    target.CurrentSession.PullRequest,
                    FilePath,
                    Arg.Any<IReadOnlyList<DiffChunk>>())
                    .Returns(threads);

                var file = (PullRequestSessionLiveFile)await target.GetLiveFile(FilePath, textView, textView.TextBuffer);

                Assert.AreEqual(2, file.TrackingPoints.Count);
            }

            [Test]
            public async Task MovingToNoRepositoryShouldNullOutProperties()
            {
                var textView = CreateTextView();
                var sessionService = CreateSessionService();
                var threads = new List<IInlineCommentThreadModel>();
                var teHolder = new FakeTeamExplorerServiceHolder(CreateRepositoryModel());

                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    CreateSessionService(),
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    teHolder);

                sessionService.BuildCommentThreads(
                    target.CurrentSession.PullRequest,
                    FilePath,
                    Arg.Any<IReadOnlyList<DiffChunk>>())
                    .Returns(threads);

                var file = (PullRequestSessionLiveFile)await target.GetLiveFile(FilePath, textView, textView.TextBuffer);

                Assert.NotNull(file.BaseSha);
                Assert.NotNull(file.CommitSha);
                Assert.NotNull(file.Diff);
                Assert.NotNull(file.InlineCommentThreads);
                Assert.NotNull(file.TrackingPoints);

                teHolder.ActiveRepo = null;

                Assert.Null(file.BaseSha);
                Assert.Null(file.CommitSha);
                Assert.Null(file.Diff);
                Assert.Null(file.InlineCommentThreads);
                Assert.Null(file.TrackingPoints);
            }

            [Test]
            public async Task ModifyingBufferMarksThreadsAsStaleAndSignalsRebuild()
            {
                var textView = CreateTextView();
                var sessionService = CreateSessionService();
                var rebuild = Substitute.For<ISubject<ITextSnapshot, ITextSnapshot>>();
                sessionService.CreateRebuildSignal().Returns(rebuild);

                var threads = new List<IInlineCommentThreadModel>
                {
                    CreateInlineCommentThreadModel(1),
                    CreateInlineCommentThreadModel(2),
                };

                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    sessionService,
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));

                sessionService.BuildCommentThreads(
                    target.CurrentSession.PullRequest,
                    FilePath,
                    Arg.Any<IReadOnlyList<DiffChunk>>())
                    .Returns(threads);

                var file = (PullRequestSessionLiveFile)await target.GetLiveFile(FilePath, textView, textView.TextBuffer);
                var linesChangedReceived = false;
                file.LinesChanged.Subscribe(x => linesChangedReceived = true);

                // Make the first tracking points return a different value so that the thread is marked as stale.
                var snapshot = textView.TextSnapshot;
                file.TrackingPoints[file.InlineCommentThreads[0]].GetPosition(snapshot).ReturnsForAnyArgs(5);

                SignalTextChanged(textView.TextBuffer);

                threads[0].Received().IsStale = true;
                threads[1].DidNotReceive().IsStale = true;

                Assert.True(linesChangedReceived);
                file.Rebuild.Received().OnNext(Arg.Any<ITextSnapshot>());
            }

            [Test]
            public async Task RebuildSignalUpdatesCommitSha()
            {
                var textView = CreateTextView();
                var sessionService = CreateSessionService();
                sessionService.CreateRebuildSignal().Returns(new Subject<ITextSnapshot>());

                var threads = new List<IInlineCommentThreadModel>
                {
                    CreateInlineCommentThreadModel(1),
                    CreateInlineCommentThreadModel(2),
                };

                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    sessionService,
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));
                var file = (PullRequestSessionLiveFile)await target.GetLiveFile(FilePath, textView, textView.TextBuffer);

                Assert.That("TIPSHA", Is.SameAs(file.CommitSha));

                sessionService.IsUnmodifiedAndPushed(null, null, null).ReturnsForAnyArgs(false);
                file.Rebuild.OnNext(textView.TextBuffer.CurrentSnapshot);

                Assert.Null(file.CommitSha);
            }

            [Test]
            public async Task ClosingTextViewDisposesFile()
            {
                var textView = CreateTextView();

                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    CreateSessionService(),
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));
                var file = (PullRequestSessionLiveFile)await target.GetLiveFile(FilePath, textView, textView.TextBuffer);

                var compositeDisposable = file.ToDispose as CompositeDisposable;
                Assert.NotNull(compositeDisposable);
                Assert.False(compositeDisposable.IsDisposed);

                textView.Closed += Raise.Event();

                Assert.True(compositeDisposable.IsDisposed);
            }

            [Test]
            public async Task InlineCommentThreadsAreLoadedFromCurrentSession()
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var contents = @"Line 1
Line 2
Line 3 with comment
Line 4";
                var comment = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment");

                using (var diffService = new FakeDiffService())
                {
                    var textView = CreateTextView(contents);
                    var pullRequest = CreatePullRequestModel(
                        CurrentBranchPullRequestNumber,
                        OwnerCloneUrl,
                        comment);

                    diffService.AddFile(FilePath, baseContents, "MERGE_BASE");

                    var target = new PullRequestSessionManager(
                        CreatePullRequestService(),
                        CreateRealSessionService(diff: diffService),
                        CreateConnectionManager(),
                        CreateModelServiceFactory(pullRequest),
                        new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));
                    var file = (PullRequestSessionLiveFile)await target.GetLiveFile(FilePath, textView, textView.TextBuffer);

                    Assert.AreEqual(1, file.InlineCommentThreads.Count);
                    Assert.AreEqual(2, file.InlineCommentThreads[0].LineNumber);
                }
            }

            [Test]
            public async Task UpdatesInlineCommentThreadsFromEditorContent()
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var contents = @"Line 1
Line 2
Line 3 with comment
Line 4";
                var editorContents = @"New Line 1
New Line 2
Line 1
Line 2
Line 3 with comment
Line 4";
                var comment = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment");

                using (var diffService = new FakeDiffService())
                {
                    var textView = CreateTextView(contents);
                    var pullRequest = CreatePullRequestModel(
                        CurrentBranchPullRequestNumber,
                        OwnerCloneUrl,
                        comment);

                    diffService.AddFile(FilePath, baseContents, "MERGE_BASE");

                    var target = new PullRequestSessionManager(
                        CreatePullRequestService(),
                        CreateRealSessionService(diff: diffService),
                        CreateConnectionManager(),
                        CreateModelServiceFactory(pullRequest),
                        new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));
                    var file = (PullRequestSessionLiveFile)await target.GetLiveFile(FilePath, textView, textView.TextBuffer);

                    Assert.AreEqual(1, file.InlineCommentThreads.Count);
                    Assert.AreEqual(2, file.InlineCommentThreads[0].LineNumber);

                    textView.TextSnapshot.GetText().Returns(editorContents);
                    SignalTextChanged(textView.TextBuffer);

                    var linesChanged = await file.LinesChanged.Take(1);

                    Assert.AreEqual(1, file.InlineCommentThreads.Count);
                    Assert.AreEqual(4, file.InlineCommentThreads[0].LineNumber);
                    Assert.AreEqual(
                        new[]
                        {
                            Tuple.Create(2, DiffSide.Right),
                            Tuple.Create(4, DiffSide.Right),
                        },
                        linesChanged.ToArray());
                }
            }

            [Test]
            public async Task UpdatesReviewCommentWithNewBody()
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var contents = @"Line 1
Line 2
Line 3 with comment
Line 4";
                var comment = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment", "Original Comment");
                var updatedComment = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment", "Updated Comment");

                using (var diffService = new FakeDiffService())
                {
                    var textView = CreateTextView(contents);
                    var pullRequest = CreatePullRequestModel(
                        CurrentBranchPullRequestNumber,
                        OwnerCloneUrl,
                        comment);

                    diffService.AddFile(FilePath, baseContents, "MERGE_BASE");

                    var target = new PullRequestSessionManager(
                        CreatePullRequestService(),
                        CreateRealSessionService(diff: diffService),
                        CreateConnectionManager(),
                        CreateModelServiceFactory(pullRequest),
                        new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));
                    var file = (PullRequestSessionLiveFile)await target.GetLiveFile(FilePath, textView, textView.TextBuffer);

                    Assert.AreEqual("Original Comment", file.InlineCommentThreads[0].Comments[0].Body);

                    pullRequest = CreatePullRequestModel(
                        CurrentBranchPullRequestNumber,
                        OwnerCloneUrl,
                        updatedComment);
                    await target.CurrentSession.Update(pullRequest);

                    await file.LinesChanged.Take(1);

                    Assert.AreEqual("Updated Comment", file.InlineCommentThreads[0].Comments[0].Body);
                }
            }

            [Test]
            public async Task AddsNewReviewCommentToThread()
            {
                var baseContents = @"Line 1
Line 2
Line 3
Line 4";
                var contents = @"Line 1
Line 2
Line 3 with comment
Line 4";
                var comment1 = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment", "Comment1");

                var comment2 = CreateComment(@"@@ -1,4 +1,4 @@
 Line 1
 Line 2
-Line 3
+Line 3 with comment", "Comment2");

                using (var diffService = new FakeDiffService())
                {
                    var textView = CreateTextView(contents);
                    var pullRequest = CreatePullRequestModel(
                        CurrentBranchPullRequestNumber,
                        OwnerCloneUrl,
                        comment1);

                    diffService.AddFile(FilePath, baseContents, "MERGE_BASE");

                    var target = new PullRequestSessionManager(
                        CreatePullRequestService(),
                        CreateRealSessionService(diff: diffService),
                        CreateConnectionManager(),
                        CreateModelServiceFactory(pullRequest),
                        new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));
                    var file = (PullRequestSessionLiveFile)await target.GetLiveFile(FilePath, textView, textView.TextBuffer);

                    Assert.AreEqual(1, file.InlineCommentThreads[0].Comments.Count);

                    pullRequest = CreatePullRequestModel(
                        CurrentBranchPullRequestNumber,
                        OwnerCloneUrl,
                        comment1,
                        comment2);
                    await target.CurrentSession.Update(pullRequest);

                    var linesChanged = await file.LinesChanged.Take(1);

                    Assert.AreEqual(2, file.InlineCommentThreads[0].Comments.Count);
                    Assert.AreEqual("Comment1", file.InlineCommentThreads[0].Comments[0].Body);
                    Assert.AreEqual("Comment2", file.InlineCommentThreads[0].Comments[1].Body);
                }
            }

            [Test]
            public async Task CommitShaIsUpdatedOnTextChange()
            {
                var textView = CreateTextView();
                var sessionService = CreateSessionService();

                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    sessionService,
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));
                var file = await target.GetLiveFile(FilePath, textView, textView.TextBuffer);

                Assert.AreEqual("TIPSHA", file.CommitSha);

                sessionService.IsUnmodifiedAndPushed(null, null, null).ReturnsForAnyArgs(false);
                SignalTextChanged(textView.TextBuffer);

                Assert.Null(file.CommitSha);
            }

            [Test]
            public async Task UpdatingCurrentSessionPullRequestTriggersLinesChanged()
            {
                var textView = CreateTextView();
                var sessionService = CreateSessionService();
                var expectedLineNumber = 2;
                var threads = new[]
                {
                    CreateInlineCommentThreadModel(expectedLineNumber),
                };

                sessionService.BuildCommentThreads(null, null, null).ReturnsForAnyArgs(threads);

                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    sessionService,
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));
                var file = await target.GetLiveFile(FilePath, textView, textView.TextBuffer);
                var raised = false;
                var pullRequest = target.CurrentSession.PullRequest;

                file.LinesChanged.Subscribe(x => raised = x.Count == 1 && x[0].Item1 == expectedLineNumber);

                // LinesChanged should be raised even if the IPullRequestModel is the same.
                await target.CurrentSession.Update(target.CurrentSession.PullRequest);

                Assert.True(raised);
            }

            static IPullRequestReviewCommentModel CreateComment(
                string diffHunk,
                string body = "Comment",
                string filePath = FilePath)
            {
                var result = Substitute.For<IPullRequestReviewCommentModel>();
                result.Body.Returns(body);
                result.DiffHunk.Returns(diffHunk);
                result.Path.Returns(filePath);
                result.OriginalCommitId.Returns("ORIG");
                result.OriginalPosition.Returns(1);
                return result;
            }

            IPullRequestSessionService CreateSessionService(bool isModified = false)
            {
                var sessionService = Substitute.For<IPullRequestSessionService>();
                sessionService.CreateRebuildSignal().Returns(new Subject<ITextSnapshot>());
                sessionService.IsUnmodifiedAndPushed(null, null, null).ReturnsForAnyArgs(!isModified);
                sessionService.GetPullRequestMergeBase(null, null).ReturnsForAnyArgs("MERGE_BASE");
                sessionService.GetTipSha(null).ReturnsForAnyArgs("TIPSHA");
                return sessionService;
            }

            IPullRequestSessionService CreateRealSessionService(IDiffService diff)
            {
                var result = Substitute.ForPartsOf<PullRequestSessionService>(
                    Substitute.For<IGitService>(),
                    Substitute.For<IGitClient>(),
                    diff,
                    Substitute.For<IApiClientFactory>(),
                    Substitute.For<IUsageTracker>());
                result.CreateRebuildSignal().Returns(new Subject<ITextSnapshot>());
                result.GetPullRequestMergeBase(Arg.Any<ILocalRepositoryModel>(), Arg.Any<IPullRequestModel>())
                    .Returns("MERGE_BASE");
                return result;
            }

            ITextView CreateTextView(string content = "Default content")
            {
                var snapshot = Substitute.For<ITextSnapshot>();
                snapshot.GetText().Returns(content);

                // We map snapshot positions to line numbers in tracking points, so return 100 as
                // the length so we can map lines 0-99.
                snapshot.Length.Returns(100);
                snapshot.GetLineFromLineNumber(0).ReturnsForAnyArgs(x =>
                {
                    var line = Substitute.For<ITextSnapshotLine>();
                    var lineNumber = x.Arg<int>();
                    var point = new SnapshotPoint(snapshot, lineNumber);
                    line.LineNumber.Returns(lineNumber);
                    line.Start.Returns(point);
                    return line;
                });
                snapshot.GetLineNumberFromPosition(0).ReturnsForAnyArgs(x => x.Arg<int>());
                snapshot.CreateTrackingPoint(0, 0).ReturnsForAnyArgs(x =>
                {
                    var point = Substitute.For<ITrackingPoint>();
                    point.GetPosition(snapshot).Returns(x.Arg<int>());
                    return point;
                });

                var textBuffer = Substitute.For<ITextBuffer>();
                textBuffer.Properties.Returns(new PropertyCollection());
                textBuffer.CurrentSnapshot.Returns(snapshot);

                var result = Substitute.For<ITextView>();
                result.TextBuffer.Returns(textBuffer);
                result.TextSnapshot.Returns(snapshot);

                return result;
            }

            IInlineCommentThreadModel CreateInlineCommentThreadModel(int lineNumber)
            {
                var result = Substitute.For<IInlineCommentThreadModel>();
                result.LineNumber.Returns(lineNumber);
                return result;
            }

            static void SignalTextChanged(ITextBuffer buffer)
            {
                var snapshot = buffer.CurrentSnapshot;
                var ev = new TextContentChangedEventArgs(snapshot, snapshot, EditOptions.None, null);
                buffer.Changed += Raise.EventWith(buffer, ev);
            }
        }

        public class TheGetSessionMethod : PullRequestSessionManagerTests
        {
            [Test]
            public async Task GetSessionReturnsAndUpdatesCurrentSessionIfNumbersMatch()
            {
                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    Substitute.For<IPullRequestSessionService>(),
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));

                var newModel = CreatePullRequestModel(CurrentBranchPullRequestNumber);
                var result = await target.GetSession(newModel);

                Assert.That(target.CurrentSession, Is.SameAs(result));
                ///Assert.That(target.CurrentSession, Is.SameAs(newModel));
            }

            [Test]
            public async Task GetSessionReturnsNewSessionForPullRequestWithDifferentNumber()
            {
                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    Substitute.For<IPullRequestSessionService>(),
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));

                var newModel = CreatePullRequestModel(NotCurrentBranchPullRequestNumber);
                var result = await target.GetSession(newModel);

                Assert.That(target.CurrentSession, Is.Not.SameAs(result));
                Assert.That(result.PullRequest, Is.SameAs(newModel));
                Assert.False(result.IsCheckedOut);
            }

            [Test]
            public async Task GetSessionReturnsNewSessionForPullRequestWithDifferentBaseOwner()
            {
                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    Substitute.For<IPullRequestSessionService>(),
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));

                var newModel = CreatePullRequestModel(CurrentBranchPullRequestNumber, "https://github.com/fork/repo");
                var result = await target.GetSession(newModel);

                Assert.That(target.CurrentSession, Is.Not.SameAs(result));
                Assert.That(result.PullRequest, Is.SameAs(newModel));
                Assert.False(result.IsCheckedOut);
            }

            [Test]
            public async Task GetSessionReturnsSameSessionEachTime()
            {
                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    Substitute.For<IPullRequestSessionService>(),
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));

                var newModel = CreatePullRequestModel(NotCurrentBranchPullRequestNumber);
                var result1 = await target.GetSession(newModel);
                var result2 = await target.GetSession(newModel);

                Assert.That(result1, Is.SameAs(result2));
            }

            [Test]
            public async Task SessionCanBeCollected()
            {
                WeakReference<IPullRequestSession> weakSession = null;

                var target = new PullRequestSessionManager(
                    CreatePullRequestService(),
                    Substitute.For<IPullRequestSessionService>(),
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));

                Func<Task> run = async () =>
                {
                    var newModel = CreatePullRequestModel(NotCurrentBranchPullRequestNumber);
                    var session = await target.GetSession(newModel);

                    Assert.NotNull(session);

                    weakSession = new WeakReference<IPullRequestSession>(session);
                };

                await run();
                GC.Collect();

                IPullRequestSession result;
                weakSession.TryGetTarget(out result);

                Assert.Null(result);
            }

            [Test]
            public async Task GetSessionUpdatesCurrentSessionIfCurrentBranchIsPullRequestButWasNotMarked()
            {
                var service = CreatePullRequestService();

                service.GetPullRequestForCurrentBranch(null).ReturnsForAnyArgs(Observable.Empty<Tuple<string, int>>());

                var target = new PullRequestSessionManager(
                    service,
                    Substitute.For<IPullRequestSessionService>(),
                    CreateConnectionManager(),
                    CreateModelServiceFactory(),
                    new FakeTeamExplorerServiceHolder(CreateRepositoryModel()));

                Assert.Null(target.CurrentSession);

                var model = CreatePullRequestModel(CurrentBranchPullRequestNumber);

                service.EnsureLocalBranchesAreMarkedAsPullRequests(Arg.Any<ILocalRepositoryModel>(), model).Returns(Observable.Return(true));
                service.GetPullRequestForCurrentBranch(null).ReturnsForAnyArgs(Observable.Return(Tuple.Create("owner", CurrentBranchPullRequestNumber)));

                var session = await target.GetSession(model);

                Assert.That(session, Is.SameAs(target.CurrentSession));
            }
        }

        IPullRequestModel CreatePullRequestModel(
            int number,
            string cloneUrl = OwnerCloneUrl,
            params IPullRequestReviewCommentModel[] comments)
        {
            var result = Substitute.For<IPullRequestModel>();
            result.Number.Returns(number);
            result.Base.Returns(new GitReferenceModel("BASEREF", "pr", "BASESHA", cloneUrl));
            result.Head.Returns(new GitReferenceModel("HEADREF", "pr", "HEADSHA", cloneUrl));
            result.ReviewComments.Returns(comments);
            return result;
        }

        IPullRequestService CreatePullRequestService(string owner = "owner")
        {
            var result = Substitute.For<IPullRequestService>();
            result.GetPullRequestForCurrentBranch(null).ReturnsForAnyArgs(Observable.Return(Tuple.Create(owner, CurrentBranchPullRequestNumber)));
            return result;
        }

        IConnectionManager CreateConnectionManager()
        {
            var connection = Substitute.For<IConnection>();
            connection.HostAddress.Returns(HostAddress.Create("https://github.com"));
            connection.IsLoggedIn.Returns(true);

            var result = Substitute.For<IConnectionManager>();
            result.GetConnection(connection.HostAddress).Returns(connection);
            return result;
        }

        IModelServiceFactory CreateModelServiceFactory(IPullRequestModel pullRequest = null)
        {
            var modelService = Substitute.For<IModelService>();
            modelService.GetPullRequest(null, null, 0).ReturnsForAnyArgs(x =>
            {
                var cloneUrl = $"https://github.com/{x.ArgAt<string>(0)}/{x.ArgAt<string>(1)}";
                var pr = pullRequest ?? CreatePullRequestModel(x.ArgAt<int>(2), cloneUrl);
                return Observable.Return(pr);
            });

            var factory = Substitute.For<IModelServiceFactory>();
            factory.CreateAsync(null).ReturnsForAnyArgs(modelService);
            factory.CreateBlocking(null).ReturnsForAnyArgs(modelService);
            return factory;
        }

        ILocalRepositoryModel CreateRepositoryModel(string cloneUrl = OwnerCloneUrl)
        {
            var result = Substitute.For<ILocalRepositoryModel>();
            var uriString = new UriString(cloneUrl);
            result.CloneUrl.Returns(uriString);
            result.Name.Returns(uriString.RepositoryName);
            result.Owner.Returns(uriString.Owner);
            return result;
        }
    }
}
