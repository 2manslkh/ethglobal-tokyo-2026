import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


SCRIPT = Path(__file__).with_name("build-hosting.py")
spec = importlib.util.spec_from_file_location("hosting", SCRIPT)
hosting = importlib.util.module_from_spec(spec)
spec.loader.exec_module(hosting)


class HostingTests(unittest.TestCase):
    def test_selected_project_metadata_uses_its_public_image_urls(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            hosting.build(output, origin="https://tagtag-tokyo-2026.web.app")
            metadata = json.loads((output / "nft/v1/taggi-1.json").read_text())
            self.assertEqual(metadata["image"],
                             "https://tagtag-tokyo-2026.web.app/nft/v1/images/taggi-1.png")

    def test_public_metadata_resolves_to_exact_bundled_artwork(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            hosting.build(output)
            for pose in range(1, 5):
                metadata = json.loads((output / f"nft/v1/taggi-{pose}.json").read_text())
                self.assertEqual(set(metadata), {"name", "description", "image"})
                self.assertEqual(metadata["image"], f"{hosting.ORIGIN}/nft/v1/images/taggi-{pose}.png")
                self.assertEqual((output / f"nft/v1/images/taggi-{pose}.png").read_bytes(),
                                 (hosting.PRESETS / f"taggi-{pose}.png").read_bytes())
            self.assertEqual(len(list(output.rglob("*.*"))), 8)

    def test_rebuild_is_idempotent_and_refuses_changed_published_content(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            hosting.build(output)
            hosting.build(output)
            published = output / "nft/v1/taggi-1.json"
            published.write_text("published content")
            with self.assertRaisesRegex(ValueError, "new version"):
                hosting.build(output)
            self.assertEqual(published.read_text(), "published content")


if __name__ == "__main__":
    unittest.main()
