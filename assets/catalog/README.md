# Bundled target catalog

`edc-active-satellites.csv` is copied unchanged from Arctra commit
[`7b0c2a7`](https://github.com/jonleithe/arctra/blob/7b0c2a701a76ec64e1ce62a43ff10e7f43459456/data/edc-active-satellites.csv).
Arctra records this as a 144-entry EDC active tracking catalog snapshot retrieved
on **2026-11-13**, from <https://edc.dgfi.tum.de/en/satellites/>.
See [Arctra's catalog documentation](https://github.com/jonleithe/arctra/blob/7b0c2a701a76ec64e1ce62a43ff10e7f43459456/docs/edc-active-satellite-catalog.md).

This is reference metadata, not current CPF availability, supported CPF format,
or current visibility. Entries with NORAD IDs are selectable CPF candidates;
entries without NORAD IDs are catalog-only lunar targets, matching Arctra's
selection rule. The backend must still acquire and validate each candidate's CPF.

IDs remain strings in the UI so leading zeros are preserved. This is Arctra's
simple four-column CSV format, not a general quoted-field CSV format. Refresh
of the catalog itself is future work; the app reads no remote catalog at startup.
The live host now acquires and validates current CPF files for selected targets;
their live availability is separate from this bundled reference list.
