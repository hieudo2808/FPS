# Known Editor issue during knowledge refresh

On 2026-09-03, Unity 6000.5.6f1 logged:

    NullReferenceException: Object reference not set to an instance of an object
    at UnityEditor.Graphs.Edge.WakeUp()
    at UnityEditor.Graphs.Graph.OnEnable()

Evidence from Logs/Editor.log:

- The stack appeared immediately after assembly/domain reload, before the semantic export started.
- It appeared again after the exporter compile, before the next export.
- The export then completed with 0 warnings.
- Unity-Skills health self-test returned OK.
- The active scene remained Assets/FPS/Scenes/MainMenu.unity, loaded, and isDirty=False.

Current conclusion: likely an existing Unity Editor graph-window/serialized-graph issue or a side effect of domain reload; exporter causality is not established. Do not edit Animator controllers or serialized assets solely to suppress this stack. Re-check the Console after a normal Editor restart or when opening the suspected graph window before taking corrective action.
