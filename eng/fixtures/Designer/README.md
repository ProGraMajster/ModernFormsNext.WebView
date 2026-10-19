# Visual Studio design-time qualification

Open `WebViewPlaceholder.mfdesign` in a separately launched Visual Studio Experimental
Instance with its already installed ModernFormsNext Designer. Do not install or update
an extension, change hosting preferences, save generated code into the framework, or
execute a project constructor to obtain a preview.

Record the VS/extension version, editor and exact owned DesignerHost PID, visible
placeholder or explicit diagnostic, and host logs. Check the owned process tree for a
browser engine process. The deliberately invalid `SourceAddress` must remain data.
An unknown-control placeholder in an older extension is a **partial** result; it does
not prove editable external WebView metadata or the current Designer integration.
No engine process during a bounded observation is not a general proof of no network.

Close the test document without saving and close only the VS instance launched for
this fixture. Runtime placeholder constructors and source metadata tests are separate
from this real Visual Studio lane.
