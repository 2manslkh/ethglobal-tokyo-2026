# Official Unity package archives

These unchanged archives were downloaded from Unity's official package host after two Unity Package Manager download attempts failed. The manifest references local tarballs so initial import does not repeat those downloads. Other dependencies still require network access.

Source URL pattern: `https://download.packages.unity.com/<package>/-/<package>-<version>.tgz`.

Integrity: all archives passed `gzip -t`. SHA-1 hashes (for reproducibility, not a security signature):

| Archive | SHA-1 |
| --- | --- |
| com.unity.inputsystem-1.20.0.tgz | 7a4e1a2a81941121ffc0721a14009b46299dada1 |
| com.unity.xr.arcore-6.5.1.tgz | 35fea30a2719c8568de1ceba430f25f316a3c255 |
| com.unity.xr.arfoundation-6.5.1.tgz | c844efe2a48d8710fc1fe3db27c9d90408b1340e |
| com.unity.xr.arkit-6.5.1.tgz | 90d15112116df192e4dd02a22d487e96ce201b16 |

AR Foundation and Input System hashes were also compared with Unity registry metadata. AR Foundation 6.1.1 and Input System 1.14.2 were rejected because they use editor APIs removed in Unity 6000.5. Each package retains its own license inside the archive. To return to registry resolution, replace the four `file:ThirdParty/...` manifest values with their listed version strings.
