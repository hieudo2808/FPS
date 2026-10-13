# FPS local SDK patch

This is an embedded copy of Multiplayer Services 2.2.4. Keep its upstream
license and package metadata. Embedding makes the fix persist across clean
checkouts and Unity's package-cache regeneration.

`Runtime/Multiplayer/Modules/Network/Handlers/NetworkHandler/NetworkManagerSession.cs`:

- Shutdown cancellation completes the captured stop task as cancelled instead of
  throwing from `ActionScheduler`, which previously logged an unhandled
  `[ServicesCore] OperationCanceledException` and left the stop task pending.
- Stop completion and disposal cancel pending lifecycle scheduler actions. A
  completed shutdown must not retain a callback with `Application.exitCancellationToken`.

Regression coverage lives in `Assets/Tests/Editor/ServicesShutdownRegressionTests.cs`.
When upgrading this SDK, check whether upstream has fixed these callbacks before
removing or rebasing the embedded package. Multiplayer 2.3.3 still schedules
`cancellationToken.ThrowIfCancellationRequested` for shutdown.
