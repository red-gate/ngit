/*
This code is derived from jgit (http://eclipse.org/jgit).
Copyright owners are documented in jgit's IP log.

This program and the accompanying materials are made available
under the terms of the Eclipse Distribution License v1.0 which
accompanies this distribution, is reproduced below, and is
available at http://www.eclipse.org/org/documents/edl-v10.php

All rights reserved.

Redistribution and use in source and binary forms, with or
without modification, are permitted provided that the following
conditions are met:

- Redistributions of source code must retain the above copyright
  notice, this list of conditions and the following disclaimer.

- Redistributions in binary form must reproduce the above
  copyright notice, this list of conditions and the following
  disclaimer in the documentation and/or other materials provided
  with the distribution.

- Neither the name of the Eclipse Foundation, Inc. nor the
  names of its contributors may be used to endorse or promote
  products derived from this software without specific prior
  written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND
CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using NGit;
using NGit.Api;
using NGit.Dircache;
using NGit.Merge;
using NGit.Revwalk;
using NGit.Storage.File;
using Sharpen;

namespace NGit.Api
{
	/// <summary>Test cherry-pick command</summary>
	[NUnit.Framework.TestFixture]
	public class CherryPickCommandTest : RepositoryTestCase
	{
		/// <exception cref="System.IO.IOException"></exception>
		/// <exception cref="NGit.Api.Errors.JGitInternalException"></exception>
		/// <exception cref="NGit.Api.Errors.GitAPIException"></exception>
		[NUnit.Framework.Test]
		public virtual void TestCherryPick()
		{
			Git git = new Git(db);
			WriteTrashFile("a", "first line\nsec. line\nthird line\n");
			git.Add().AddFilepattern("a").Call();
			RevCommit firstCommit = git.Commit().SetMessage("create a").Call();
			WriteTrashFile("b", "content\n");
			git.Add().AddFilepattern("b").Call();
			git.Commit().SetMessage("create b").Call();
			WriteTrashFile("a", "first line\nsec. line\nthird line\nfourth line\n");
			git.Add().AddFilepattern("a").Call();
			git.Commit().SetMessage("enlarged a").Call();
			WriteTrashFile("a", "first line\nsecond line\nthird line\nfourth line\n");
			git.Add().AddFilepattern("a").Call();
			RevCommit fixingA = git.Commit().SetMessage("fixed a").Call();
			git.BranchCreate().SetName("side").SetStartPoint(firstCommit).Call();
			CheckoutBranch("refs/heads/side");
			WriteTrashFile("a", "first line\nsec. line\nthird line\nfeature++\n");
			git.Add().AddFilepattern("a").Call();
			git.Commit().SetMessage("enhanced a").Call();
			git.CherryPick().Include(fixingA).Call();
			NUnit.Framework.Assert.IsFalse(new FilePath(db.WorkTree, "b").Exists());
			CheckFile(new FilePath(db.WorkTree, "a"), "first line\nsecond line\nthird line\nfeature++\n"
				);
			Iterator<RevCommit> history = git.Log().Call().Iterator();
			NUnit.Framework.Assert.AreEqual("fixed a", history.Next().GetFullMessage());
			NUnit.Framework.Assert.AreEqual("enhanced a", history.Next().GetFullMessage());
			NUnit.Framework.Assert.AreEqual("create a", history.Next().GetFullMessage());
			NUnit.Framework.Assert.IsFalse(history.HasNext());
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestCherryPickDirtyIndex()
		{
			Git git = new Git(db);
			RevCommit sideCommit = PrepareCherryPick(git);
			// modify and add file a
			WriteTrashFile("a", "a(modified)");
			git.Add().AddFilepattern("a").Call();
			// do not commit
			DoCherryPickAndCheckResult(git, sideCommit, ResolveMerger.MergeFailureReason.DIRTY_INDEX
				);
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestCherryPickDirtyWorktree()
		{
			Git git = new Git(db);
			RevCommit sideCommit = PrepareCherryPick(git);
			// modify file a
			WriteTrashFile("a", "a(modified)");
			// do not add and commit
			DoCherryPickAndCheckResult(git, sideCommit, ResolveMerger.MergeFailureReason.DIRTY_WORKTREE
				);
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestCherryPickConflictResolution()
		{
			Git git = new Git(db);
			RevCommit sideCommit = PrepareCherryPick(git);
			CherryPickResult result = git.CherryPick().Include(sideCommit.Id).Call();
			NUnit.Framework.Assert.AreEqual(CherryPickResult.CherryPickStatus.CONFLICTING, result
				.GetStatus());
			NUnit.Framework.Assert.IsTrue(new FilePath(db.Directory, Constants.MERGE_MSG).Exists
				());
			NUnit.Framework.Assert.AreEqual("side\n\nConflicts:\n\ta\n", db.ReadMergeCommitMsg
				());
			NUnit.Framework.Assert.IsTrue(new FilePath(db.Directory, Constants.CHERRY_PICK_HEAD
				).Exists());
			NUnit.Framework.Assert.AreEqual(sideCommit.Id, db.ReadCherryPickHead());
			NUnit.Framework.Assert.AreEqual(RepositoryState.CHERRY_PICKING, db.GetRepositoryState
				());
			// Resolve
			WriteTrashFile("a", "a");
			git.Add().AddFilepattern("a").Call();
			NUnit.Framework.Assert.AreEqual(RepositoryState.CHERRY_PICKING_RESOLVED, db.GetRepositoryState
				());
			git.Commit().SetOnly("a").SetMessage("resolve").Call();
			NUnit.Framework.Assert.AreEqual(RepositoryState.SAFE, db.GetRepositoryState());
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestCherryPickConflictReset()
		{
			Git git = new Git(db);
			RevCommit sideCommit = PrepareCherryPick(git);
			CherryPickResult result = git.CherryPick().Include(sideCommit.Id).Call();
			NUnit.Framework.Assert.AreEqual(CherryPickResult.CherryPickStatus.CONFLICTING, result
				.GetStatus());
			NUnit.Framework.Assert.AreEqual(RepositoryState.CHERRY_PICKING, db.GetRepositoryState
				());
			NUnit.Framework.Assert.IsTrue(new FilePath(db.Directory, Constants.CHERRY_PICK_HEAD
				).Exists());
			git.Reset().SetMode(ResetCommand.ResetType.MIXED).SetRef("HEAD").Call();
			NUnit.Framework.Assert.AreEqual(RepositoryState.SAFE, db.GetRepositoryState());
			NUnit.Framework.Assert.IsFalse(new FilePath(db.Directory, Constants.CHERRY_PICK_HEAD
				).Exists());
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestCherryPickOverExecutableChangeOnNonExectuableFileSystem()
		{
			Git git = new Git(db);
			FilePath file = WriteTrashFile("test.txt", "a");
			NUnit.Framework.Assert.IsNotNull(git.Add().AddFilepattern("test.txt").Call());
			NUnit.Framework.Assert.IsNotNull(git.Commit().SetMessage("commit1").Call());
			NUnit.Framework.Assert.IsNotNull(git.Checkout().SetCreateBranch(true).SetName("a"
				).Call());
			WriteTrashFile("test.txt", "b");
			NUnit.Framework.Assert.IsNotNull(git.Add().AddFilepattern("test.txt").Call());
			RevCommit commit2 = git.Commit().SetMessage("commit2").Call();
			NUnit.Framework.Assert.IsNotNull(commit2);
			NUnit.Framework.Assert.IsNotNull(git.Checkout().SetName(Constants.MASTER).Call());
			DirCache cache = db.LockDirCache();
			cache.GetEntry("test.txt").FileMode = FileMode.EXECUTABLE_FILE;
			cache.Write();
			NUnit.Framework.Assert.IsTrue(cache.Commit());
			cache.Unlock();
			NUnit.Framework.Assert.IsNotNull(git.Commit().SetMessage("commit3").Call());
			git.GetRepository().GetConfig().SetBoolean(ConfigConstants.CONFIG_CORE_SECTION, null
				, ConfigConstants.CONFIG_KEY_FILEMODE, false);
			CherryPickResult result = git.CherryPick().Include(commit2).Call();
			NUnit.Framework.Assert.IsNotNull(result);
			NUnit.Framework.Assert.AreEqual(CherryPickResult.CherryPickStatus.OK, result.GetStatus
				());
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestCherryPickConflictMarkers()
		{
			Git git = new Git(db);
			RevCommit sideCommit = PrepareCherryPick(git);
			CherryPickResult result = git.CherryPick().Include(sideCommit.Id).Call();
			NUnit.Framework.Assert.AreEqual(CherryPickResult.CherryPickStatus.CONFLICTING, result
				.GetStatus());
			string expected = "<<<<<<< master\na(master)\n=======\na(side)\n>>>>>>> 527460a side\n";
			CheckFile(new FilePath(db.WorkTree, "a"), expected);
		}

		/// <exception cref="System.Exception"></exception>
		[NUnit.Framework.Test]
		public virtual void TestCherryPickOurCommitName()
		{
			Git git = new Git(db);
			RevCommit sideCommit = PrepareCherryPick(git);
			CherryPickResult result = git.CherryPick().Include(sideCommit.Id).SetOurCommitName
				("custom name").Call();
			NUnit.Framework.Assert.AreEqual(CherryPickResult.CherryPickStatus.CONFLICTING, result
				.GetStatus());
			string expected = "<<<<<<< custom name\na(master)\n=======\na(side)\n>>>>>>> 527460a side\n";
			CheckFile(new FilePath(db.WorkTree, "a"), expected);
		}

		/// <exception cref="System.Exception"></exception>
		private RevCommit PrepareCherryPick(Git git)
		{
			// create, add and commit file a
			WriteTrashFile("a", "a");
			git.Add().AddFilepattern("a").Call();
			RevCommit firstMasterCommit = git.Commit().SetMessage("first master").Call();
			// create and checkout side branch
			CreateBranch(firstMasterCommit, "refs/heads/side");
			CheckoutBranch("refs/heads/side");
			// modify, add and commit file a
			WriteTrashFile("a", "a(side)");
			git.Add().AddFilepattern("a").Call();
			RevCommit sideCommit = git.Commit().SetMessage("side").Call();
			// checkout master branch
			CheckoutBranch("refs/heads/master");
			// modify, add and commit file a
			WriteTrashFile("a", "a(master)");
			git.Add().AddFilepattern("a").Call();
			git.Commit().SetMessage("second master").Call();
			return sideCommit;
		}

		/// <exception cref="System.Exception"></exception>
		private void DoCherryPickAndCheckResult(Git git, RevCommit sideCommit, ResolveMerger.MergeFailureReason
			 reason)
		{
			// get current index state
			string indexState = IndexState(CONTENT);
			// cherry-pick
			CherryPickResult result = git.CherryPick().Include(sideCommit.Id).Call();
			NUnit.Framework.Assert.AreEqual(CherryPickResult.CherryPickStatus.FAILED, result.
				GetStatus());
			// staged file a causes DIRTY_INDEX
			NUnit.Framework.Assert.AreEqual(1, result.GetFailingPaths().Count);
			NUnit.Framework.Assert.AreEqual(reason, result.GetFailingPaths().Get("a"));
			NUnit.Framework.Assert.AreEqual("a(modified)", Read(new FilePath(db.WorkTree, "a"
				)));
			// index shall be unchanged
			NUnit.Framework.Assert.AreEqual(indexState, IndexState(CONTENT));
			NUnit.Framework.Assert.AreEqual(RepositoryState.SAFE, db.GetRepositoryState());
			if (reason == null)
			{
				ReflogReader reader = db.GetReflogReader(Constants.HEAD);
				NUnit.Framework.Assert.IsTrue(reader.GetLastEntry().GetComment().StartsWith("cherry-pick: "
					));
				reader = db.GetReflogReader(db.GetBranch());
				NUnit.Framework.Assert.IsTrue(reader.GetLastEntry().GetComment().StartsWith("cherry-pick: "
					));
			}
		}
	}
}
