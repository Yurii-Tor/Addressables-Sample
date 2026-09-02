# Third-party notices

The MIT license in `LICENSE` covers the source code, the Editor tooling, and the
generated content of this repository. It does **not** cover the third-party
material listed below.

## Round textures — `Assets/Textures/icons8-*.png`

The ten animal PNGs were supplied as part of the original test-task package. Their
filenames carry an `icons8-` prefix, which indicates they originate from
[Icons8](https://icons8.com). Neither the task package nor its source PDF included a
creator name, a source URL, or license terms, so this repository makes no license
claim on them.

Icons8 assets on the free tier normally require visible attribution; paid plans
remove that requirement. Because the applicable plan is unknown, these files are
kept here **solely to reproduce the original submission**.

If you fork this repository or reuse it in your own work, either:

1. confirm the applicable Icons8 license and add the attribution it requires, or
2. replace the contents of `Assets/Textures/` with images you have the right to use,
   keeping the ten filenames listed in `TestTaskPaths.RoundStems`, and re-run
   **AddressablesSample > Game > Setup Test Task**.

## Unity packages

Unity, the Universal Render Pipeline, Addressables, the Input System, and the Unity
Test Framework are distributed by Unity Technologies under the Unity Package
Distribution terms. They are referenced through `Packages/manifest.json` and are not
redistributed in this repository.

## `.gitignore`

`.gitignore` is derived from [github/gitignore](https://github.com/github/gitignore)
(`Unity.gitignore`), distributed under CC0-1.0.
