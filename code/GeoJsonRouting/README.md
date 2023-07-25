This projects expects an `obstacle.geojson` file in the `Resources` folder.
This GeoJSON file must have at least one node tagged with `start=...` and at least one node with `destination=...` (replace `...` with anything, the exact value here is irrelevant).
The agent created in this project will then travel from the start to the destination node.

**Note:**
This was just a test and playground project and was not further used (the `HikerModel` project is up-to-date and was frequently used).

### Run it

Starting this project works by just hitting the play/run button.

### Example datasets

Take a look at the [`Resources`](./GeoJsonRouting/Resources) folder, it contains several example datasets including real world OpenStreetMap dumps.
