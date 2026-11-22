# Super Metroid Samus for OpenARNF

A Robot Named Fight © 2017 Morningstar Game Studio Incorporated. This unofficial source mod uses the [OpenARNF user license](https://github.com/BartMartner/OpenARNF/blob/91cf2b01c0c5143cac9f343f859447473d201843/License.md).

The importer uses the supplied 6496×4384 Samus Aran PNG from [The Spriters Resource](https://www.spriters-resource.com/snes/smetroid/asset/182270/). The original game artwork is from Nintendo. The archive contains the importer and renderer patch. The importer reads the PNG locally.

OpenARNF's main player gets 187 sprite frames at 48×48 and 16 pixels per Unity unit. Standing height is 41 pixels in both directions. The Arachnomorph animations use 22 frames from Samus's crouch, morph, and Morph Ball art. The Dash and Dive Kick use the closest available Shinespark and downward-aim poses. Equipped weapons use the arm cannon appearance on the mapped frames.

Open **Settings → Other Options → Samus Sprite** to choose the appearance. The setting is saved per file and defaults to Off, including for existing files. Use Left, Right, or Confirm to change it.

## Apply to the Unity source project

Requires Git, Python 3.10 or later with Pillow 10.1 or later, and OpenARNF. [OpenARNF setup](https://github.com/BartMartner/OpenARNF#details)

```powershell
python -m pip install 'Pillow>=10.1'
python .\import_samus.py `
  --openarnf 'C:\path\to\OpenARNF' `
  --samus-sheet 'C:\path\to\SNES - Super Metroid - Playable Characters - Samus Aran.png' `
  --preview '.\Super-Metroid-Samus-Preview.png' `
  --apply
```

Omit `--apply` to generate the preview alone. The two generated textures go in `Assets/Resources/Samus`. The mod targets the Unity source project, commit `91cf2b0`. The installer also upgrades a checkout with the earlier Samus patch.
