1. Make sure `osmium` is installed
2. Download the region as `.osm.pbf` file of your choice (for example https://download.geofabrik.de/europe/germany-latest.osm.pbf)
3. Extract data obstacle data: `osmium tags-filter -o data.osm.pbf hamburg-latest.osm.pbf w/building w/barrier w/railway w/natural --overwrite`
    * This step might take a while, depending on the dataset size
4. Convert to OPL file: `osmium cat data.osm.pbf -o data.opl --overwrite`
5. Analyze the file: `python non-unique-x-coordinates.py data.opl`
    * Use `python non-unique-x-coordinates.py data.opl opl` to create additional OPL output of all nodes taken into account
