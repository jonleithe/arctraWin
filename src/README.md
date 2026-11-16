# Source components

The UI and prediction engine remain separate:

- `ArctraWin.App` owns the WinUI 3 window, native controls, settings, and
  WebView2 lifecycle.
- `ArctraWin.Globe` renders only versioned state supplied by the backend. It
  does not parse CPF files or reproduce orbital calculations.
- `ArctraWin.Backend` exposes Arctra's validated C++ results through a local,
  versioned interface. It retains CPF provenance and never extrapolates.
