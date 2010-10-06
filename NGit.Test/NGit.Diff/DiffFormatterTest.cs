using NGit;
using NGit.Diff;
using NGit.Junit;
using NGit.Patch;
using NGit.Util;
using NGit.Util.IO;
using Sharpen;

namespace NGit.Diff
{
	public class DiffFormatterTest : RepositoryTestCase
	{
		private static readonly string DIFF = "diff --git ";

		private static readonly string REGULAR_FILE = "100644";

		private static readonly string GITLINK = "160000";

		private static readonly string PATH_A = "src/a";

		private static readonly string PATH_B = "src/b";

		private DiffFormatter df;

		private TestRepository testDb;

		/// <exception cref="System.Exception"></exception>
		protected override void SetUp()
		{
			base.SetUp();
			testDb = new TestRepository(db);
			df = new DiffFormatter(DisabledOutputStream.INSTANCE);
			df.SetRepository(db);
			df.SetAbbreviationLength(8);
		}

		/// <exception cref="System.Exception"></exception>
		protected override void TearDown()
		{
			if (df != null)
			{
				df.Release();
			}
			base.TearDown();
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestCreateFileHeader_Add()
		{
			ObjectId adId = Blob("a\nd\n");
			DiffEntry ent = DiffEntry.Add("FOO", adId);
			FileHeader fh = df.ToFileHeader(ent);
			string diffHeader = "diff --git a/FOO b/FOO\n" + "new file mode " + REGULAR_FILE 
				+ "\n" + "index " + ObjectId.ZeroId.Abbreviate(8).Name + ".." + adId.Abbreviate(
				8).Name + "\n" + "--- /dev/null\n" + "+++ b/FOO\n";
			//
			//
			//
			NUnit.Framework.Assert.AreEqual(diffHeader, RawParseUtils.Decode(fh.GetBuffer()));
			NUnit.Framework.Assert.AreEqual(0, fh.GetStartOffset());
			NUnit.Framework.Assert.AreEqual(fh.GetBuffer().Length, fh.GetEndOffset());
			NUnit.Framework.Assert.AreEqual(FileHeader.PatchType.UNIFIED, fh.GetPatchType());
			NUnit.Framework.Assert.AreEqual(1, fh.GetHunks().Count);
			HunkHeader hh = fh.GetHunks()[0];
			NUnit.Framework.Assert.AreEqual(1, hh.ToEditList().Count);
			EditList el = hh.ToEditList();
			NUnit.Framework.Assert.AreEqual(1, el.Count);
			Edit e = el[0];
			NUnit.Framework.Assert.AreEqual(0, e.GetBeginA());
			NUnit.Framework.Assert.AreEqual(0, e.GetEndA());
			NUnit.Framework.Assert.AreEqual(0, e.GetBeginB());
			NUnit.Framework.Assert.AreEqual(2, e.GetEndB());
			NUnit.Framework.Assert.AreEqual(Edit.Type.INSERT, e.GetType());
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestCreateFileHeader_Delete()
		{
			ObjectId adId = Blob("a\nd\n");
			DiffEntry ent = DiffEntry.Delete("FOO", adId);
			FileHeader fh = df.ToFileHeader(ent);
			string diffHeader = "diff --git a/FOO b/FOO\n" + "deleted file mode " + REGULAR_FILE
				 + "\n" + "index " + adId.Abbreviate(8).Name + ".." + ObjectId.ZeroId.Abbreviate
				(8).Name + "\n" + "--- a/FOO\n" + "+++ /dev/null\n";
			//
			//
			//
			NUnit.Framework.Assert.AreEqual(diffHeader, RawParseUtils.Decode(fh.GetBuffer()));
			NUnit.Framework.Assert.AreEqual(0, fh.GetStartOffset());
			NUnit.Framework.Assert.AreEqual(fh.GetBuffer().Length, fh.GetEndOffset());
			NUnit.Framework.Assert.AreEqual(FileHeader.PatchType.UNIFIED, fh.GetPatchType());
			NUnit.Framework.Assert.AreEqual(1, fh.GetHunks().Count);
			HunkHeader hh = fh.GetHunks()[0];
			NUnit.Framework.Assert.AreEqual(1, hh.ToEditList().Count);
			EditList el = hh.ToEditList();
			NUnit.Framework.Assert.AreEqual(1, el.Count);
			Edit e = el[0];
			NUnit.Framework.Assert.AreEqual(0, e.GetBeginA());
			NUnit.Framework.Assert.AreEqual(2, e.GetEndA());
			NUnit.Framework.Assert.AreEqual(0, e.GetBeginB());
			NUnit.Framework.Assert.AreEqual(0, e.GetEndB());
			NUnit.Framework.Assert.AreEqual(Edit.Type.DELETE, e.GetType());
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestCreateFileHeader_Modify()
		{
			ObjectId adId = Blob("a\nd\n");
			ObjectId abcdId = Blob("a\nb\nc\nd\n");
			string diffHeader = MakeDiffHeader(PATH_A, PATH_A, adId, abcdId);
			DiffEntry ad = DiffEntry.Delete(PATH_A, adId);
			DiffEntry abcd = DiffEntry.Add(PATH_A, abcdId);
			DiffEntry mod = DiffEntry.Pair(DiffEntry.ChangeType.MODIFY, ad, abcd, 0);
			FileHeader fh = df.ToFileHeader(mod);
			NUnit.Framework.Assert.AreEqual(diffHeader, RawParseUtils.Decode(fh.GetBuffer()));
			NUnit.Framework.Assert.AreEqual(0, fh.GetStartOffset());
			NUnit.Framework.Assert.AreEqual(fh.GetBuffer().Length, fh.GetEndOffset());
			NUnit.Framework.Assert.AreEqual(FileHeader.PatchType.UNIFIED, fh.GetPatchType());
			NUnit.Framework.Assert.AreEqual(1, fh.GetHunks().Count);
			HunkHeader hh = fh.GetHunks()[0];
			NUnit.Framework.Assert.AreEqual(1, hh.ToEditList().Count);
			EditList el = hh.ToEditList();
			NUnit.Framework.Assert.AreEqual(1, el.Count);
			Edit e = el[0];
			NUnit.Framework.Assert.AreEqual(1, e.GetBeginA());
			NUnit.Framework.Assert.AreEqual(1, e.GetEndA());
			NUnit.Framework.Assert.AreEqual(1, e.GetBeginB());
			NUnit.Framework.Assert.AreEqual(3, e.GetEndB());
			NUnit.Framework.Assert.AreEqual(Edit.Type.INSERT, e.GetType());
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestCreateFileHeader_Binary()
		{
			ObjectId adId = Blob("a\nd\n");
			ObjectId binId = Blob("a\nb\nc\n\x0\x0\x0\x0d\n");
			string diffHeader = MakeDiffHeader(PATH_A, PATH_B, adId, binId) + "Binary files differ\n";
			DiffEntry ad = DiffEntry.Delete(PATH_A, adId);
			DiffEntry abcd = DiffEntry.Add(PATH_B, binId);
			DiffEntry mod = DiffEntry.Pair(DiffEntry.ChangeType.MODIFY, ad, abcd, 0);
			FileHeader fh = df.ToFileHeader(mod);
			NUnit.Framework.Assert.AreEqual(diffHeader, RawParseUtils.Decode(fh.GetBuffer()));
			NUnit.Framework.Assert.AreEqual(FileHeader.PatchType.BINARY, fh.GetPatchType());
			NUnit.Framework.Assert.AreEqual(1, fh.GetHunks().Count);
			HunkHeader hh = fh.GetHunks()[0];
			NUnit.Framework.Assert.AreEqual(0, hh.ToEditList().Count);
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestCreateFileHeader_GitLink()
		{
			ObjectId aId = Blob("a\n");
			ObjectId bId = Blob("b\n");
			string diffHeader = MakeDiffHeaderModeChange(PATH_A, PATH_A, aId, bId, GITLINK, REGULAR_FILE
				) + "-Subproject commit " + aId.Name + "\n";
			DiffEntry ad = DiffEntry.Delete(PATH_A, aId);
			ad.oldMode = FileMode.GITLINK;
			DiffEntry abcd = DiffEntry.Add(PATH_A, bId);
			DiffEntry mod = DiffEntry.Pair(DiffEntry.ChangeType.MODIFY, ad, abcd, 0);
			FileHeader fh = df.ToFileHeader(mod);
			NUnit.Framework.Assert.AreEqual(diffHeader, RawParseUtils.Decode(fh.GetBuffer()));
			NUnit.Framework.Assert.AreEqual(1, fh.GetHunks().Count);
			HunkHeader hh = fh.GetHunks()[0];
			NUnit.Framework.Assert.AreEqual(0, hh.ToEditList().Count);
		}

		private string MakeDiffHeader(string pathA, string pathB, ObjectId aId, ObjectId 
			bId)
		{
			string a = aId.Abbreviate(8).Name;
			string b = bId.Abbreviate(8).Name;
			return DIFF + "a/" + pathA + " " + "b/" + pathB + "\n" + "index " + a + ".." + b 
				+ " " + REGULAR_FILE + "\n" + "--- a/" + pathA + "\n" + "+++ b/" + pathB + "\n";
		}

		//
		//
		//
		private string MakeDiffHeaderModeChange(string pathA, string pathB, ObjectId aId, 
			ObjectId bId, string modeA, string modeB)
		{
			string a = aId.Abbreviate(8).Name;
			string b = bId.Abbreviate(8).Name;
			return DIFF + "a/" + pathA + " " + "b/" + pathB + "\n" + "old mode " + modeA + "\n"
				 + "new mode " + modeB + "\n" + "index " + a + ".." + b + "\n" + "--- a/" + pathA
				 + "\n" + "+++ b/" + pathB + "\n";
		}

		//
		//
		//
		//
		//
		/// <exception cref="System.Exception"></exception>
		private ObjectId Blob(string content)
		{
			return testDb.Blob(content).Copy();
		}
	}
}