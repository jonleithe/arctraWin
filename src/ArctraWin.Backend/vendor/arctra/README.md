# Arctra source subset

These files are copied unchanged from `jonleithe/arctra` commit
`7b0c2a701a76ec64e1ce62a43ff10e7f43459456`:
`src/cpf.cpp` and `include/arctra/{cpf,ephemeris,prediction,satellite}.h`.

Live geometry additionally uses unchanged `src/coordinates.cpp` and
`include/arctra/{coordinates,observatory}.h` from the same commit.

Source: <https://github.com/jonleithe/arctra/tree/7b0c2a701a76ec64e1ce62a43ff10e7f43459456>.
This subset builds the existing C++20 CPF parser and interpolation code without
the POSIX terminal or libcurl dependencies. It performs the authoritative
calculation for the snapshot exporter. Update from upstream deliberately;
do not create a separate orbital implementation in C# or TypeScript.
