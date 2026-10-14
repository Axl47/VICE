"""Integration fixtures for release baselines and player-facing summaries."""
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import os


ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("release_notes", ROOT / "scripts/Generate-ReleaseNotes.py")
notes = importlib.util.module_from_spec(spec)
spec.loader.exec_module(notes)


class ReleaseNotesTests(unittest.TestCase):
    def setUp(self):
        artifacts = ROOT / "artifacts"
        artifacts.mkdir(exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=artifacts)
        self.old_cwd = Path.cwd()
        os.chdir(self.temp.name)
        self.addCleanup(self.cleanup)
        self.run_git("init", "-q")
        self.run_git("config", "user.name", "Release fixture")
        self.run_git("config", "user.email", "fixture@example.invalid")
        self.run_git("config", "commit.gpgsign", "false")
        self.run_git("config", "tag.gpgsign", "false")
        self.write("src/VICE.Core/ModSupport.cs", "initial")
        self.write("scripts/Package-TVCompanion.ps1", "$expectedArchive = '" + "A" * 64 + "'\n$expectedHelper = '" + "B" * 64 + "'")
        self.commit("Technical baseline", "v1.0.0")

    def cleanup(self):
        os.chdir(self.old_cwd)
        self.temp.cleanup()

    def run_git(self, *args):
        return subprocess.check_output(["git", *args], stderr=subprocess.PIPE, text=True).strip()

    def write(self, path, content):
        target = Path(path)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding="utf-8")

    def commit(self, message, tag):
        self.run_git("add", ".")
        self.run_git("commit", "-qm", message)
        self.run_git("tag", tag)

    def release(self, tag, date, **kwargs):
        return dict(tag_name=tag, published_at=date, draft=False, prerelease=False, **kwargs)

    def test_new_poker_docs_companion_and_no_commit_noise(self):
        self.write("src/VICE.Lifestyle/Poker/Table.cs", "poker")
        self.write("src/VICE.Lifestyle/LifestyleMod.cs", "register")
        self.write("docs/vice-lifestyle-poker.md", "Poker remains experimental.")
        self.commit("feat: technical implementation (#123)", "v1.1.0")
        body = notes.generate("v1.1.0", notes.tag_commit("v1.1.0"), "v1.0.0", notes.tag_commit("v1.0.0"), "Axl47/VICE")
        self.assertIn("Added cash Texas Hold'em poker.", body)
        self.assertNotIn("Updated Lifestyle features", body)
        self.assertIn("experimental", body)
        self.assertIn("player guides and documentation", body)
        self.assertIn("Companion is unchanged", body)
        self.assertIn("VICE-v1.1.0.zip", body)
        self.assertIn("compare/v1.0.0...v1.1.0", body)
        self.assertNotIn("#123", body)
        self.assertNotIn("technical implementation", body)

    def test_update_existing_feature_is_not_new_and_removed_is_explicit(self):
        self.write("src/VICE.Lifestyle/Poker/Table.cs", "table")
        self.commit("add", "v1.1.0")
        self.write("src/VICE.Lifestyle/Poker/Rules.cs", "rules")
        self.commit("update", "v1.2.0")
        body = notes.generate("v1.2.0", notes.tag_commit("v1.2.0"), "v1.1.0", notes.tag_commit("v1.1.0"), "")
        self.assertIn("Updated cash Texas Hold'em poker.", body)
        self.assertNotIn("experimental", body)
        Path("src/VICE.Lifestyle/Poker/Table.cs").unlink()
        Path("src/VICE.Lifestyle/Poker/Rules.cs").unlink()
        self.commit("remove", "v1.3.0")
        body = notes.generate("v1.3.0", notes.tag_commit("v1.3.0"), "v1.2.0", notes.tag_commit("v1.2.0"), "")
        self.assertIn("Removed cash Texas Hold'em poker.", body)

    def test_published_baseline_skips_draft_current_prerelease_and_other_branch(self):
        self.write("README.md", "branch")
        self.commit("branch", "unrelated")
        self.run_git("checkout", "-q", "v1.0.0")
        self.write("README.md", "beta")
        self.commit("beta", "v1.1.0-beta.1")
        self.write("README.md", "stable")
        self.commit("stable", "v1.1.0")
        releases = [self.release("v1.0.0", "2026-01-01"), self.release("unrelated", "2026-01-02"),
                    self.release("v1.1.0-beta.1", "2026-01-03"), self.release("v1.1.0", "2026-01-04"),
                    dict(tag_name="missing-draft", draft=True, published_at="2026-01-05")]
        releases[2]["prerelease"] = True
        self.assertEqual(notes.previous_release(releases, "v1.1.0", notes.tag_commit("v1.1.0"))[0], "v1.0.0")
        self.assertEqual(notes.previous_release(releases, "v1.2.0-beta.1", notes.tag_commit("v1.1.0"))[0], "v1.1.0")

    def test_first_release_and_changed_companion(self):
        body = notes.generate("v1.0.0", notes.tag_commit("v1.0.0"), None, None, "")
        self.assertIn("Added shared settings and module support", body)
        self.assertNotIn("unchanged", body)
        self.write("scripts/Package-TVCompanion.ps1", "$expectedArchive = '" + "C" * 64 + "'\n$expectedHelper = '" + "B" * 64 + "'")
        self.commit("browser update", "v1.1.0")
        body = notes.generate("v1.1.0", notes.tag_commit("v1.1.0"), "v1.0.0", notes.tag_commit("v1.0.0"), "")
        self.assertNotIn("unchanged", body)
        self.assertIn("Updated the optional YouTube Companion.", body)

    def test_new_feature_area_is_not_mislabeled_as_maintenance(self):
        bullets = notes.summarize({"src/VICE.NewFeature/Game.cs"}, set(), {"src/VICE.NewFeature/Game.cs"})
        self.assertEqual(bullets, ["Updated other gameplay features."])

    def test_many_feature_updates_are_compact_without_omissions(self):
        paths = {prefixes[0] + "Nested/Feature.cs" for _, prefixes in notes.FEATURES}
        bullets = notes.summarize(paths, paths, paths)
        self.assertLessEqual(len(bullets), 6)
        body = " ".join(bullets)
        for label, _ in notes.FEATURES:
            self.assertIn(label, body)

    def test_missing_published_tag_fails_and_invalid_tag_rejected(self):
        with self.assertRaisesRegex(RuntimeError, "fetch all tags"):
            notes.previous_release([self.release("missing", "2026-01-01")], "v1.1.0", notes.tag_commit("v1.0.0"))
        with self.assertRaises(ValueError):
            notes.tag_commit("--upload-pack=bad")

    def test_api_paginates_and_does_not_swallow_errors(self):
        class Response:
            def __init__(self, data):
                self.data = __import__("io").StringIO(__import__("json").dumps(data))
            def __enter__(self):
                return self.data
            def __exit__(self, *args):
                self.data.close()
        with patch.object(notes.urllib.request, "urlopen", side_effect=[Response([{}] * 100), Response([{}])]) as request:
            self.assertEqual(len(notes.published_releases("Axl47/VICE")), 101)
            self.assertIn("page=2", request.call_args.args[0].full_url)
        with patch.object(notes.urllib.request, "urlopen", side_effect=OSError("offline")):
            with self.assertRaises(OSError):
                notes.published_releases("Axl47/VICE")


if __name__ == "__main__":
    unittest.main()
