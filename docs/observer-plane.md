# Observer locations and elevation limits

Choose a station using **Observer** above the satellite list. Switching stations updates its marker, local geometry and live look angles without moving the camera. The active station is remembered between launches.

Expand **Observer and tangent plane** for **New**, **Edit**, **Import INI**, **Save as**, **Reload files** and **Open folder**. Each station has its own INI under `%LOCALAPPDATA%/ArctraWin/Observers/`. Import copies a validated file into that folder; Save as creates another profile. Neither overwrites existing profiles. Edit updates the selected file. After external edits, use Reload files. Invalid files are skipped with a visible warning.

The format matches Arctra's observer section:

```ini
[observatory]
name = Ny-Aalesund SGSLR
lat = 78.94300
lon = 11.85580
height = 13.0
min-elevation = 12.5
```

Latitude/longitude are geodetic decimal degrees, north/east positive. Height is WGS 84 ellipsoidal metres. Use decimal points. All five fields are required; unrelated INI sections are ignored. Latitude accepts -90 to 90, longitude -180 to 180, height -1000 to 100000 m and minimum elevation -90 to 90 degrees. The bundled [nyal.ini](../assets/observers/nyal.ini) is approximate: replace its coordinates and limit with actual station values.

Existing saved coordinates migrate into a separate profile, preserving their location and previous zero-degree threshold. The initial nyal.ini profile uses the example above. Display preferences remain in `observer-v1.json`; the active filename is stored in `observer-profile.json`.

The blue flat patch remains the true **0-degree geometric horizon**: the local East-North plane through the observer, normal to geodetic Up. Height translates it without changing its orientation. It automatically extends beyond selected loaded orbits and CPF trails, with an 8% margin rounded to 100 km. Without loaded geometry it uses the saved fallback width. Automatic sizing has no 40000 km cap.

The translucent gold cone shows the configured **minimum elevation**, measured up from the horizontal in every azimuth. Its open rim extends beyond loaded orbits. At zero degrees it lies in the horizontal plane. Plane and cone have independent visibility controls. Green live markers meet or exceed the configured limit; markers below it are muted. Arctra's C++ backend calculates live elevation and threshold classification each second.

Use **View plane from the side**, **View observer from above**, or rotate freely to inspect the geometry. Reference orbit loops are approximate; current markers and live elevations determine the displayed threshold state. Terrain, obstructions and other telescope constraints are not included.
