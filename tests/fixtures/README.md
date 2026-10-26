# PowerPoint style fixture

`styled-native.pptx` was created locally in Microsoft PowerPoint on 2026-10-26 with an original test slide: a navy background, a rotated rounded rectangle with custom fill/outline, and centered white Georgia italic text. It contains no user content.

`styled-native-reference.png` is PowerPoint's direct `Slide.Export(..., "PNG", 1440, 810)` output. The optional `-NativePowerPoint` tests compare decoded pixels against it, check that the source file stays unchanged, and exercise cancellation and an existing automation session. Run those tests with PowerPoint installed and closed.
