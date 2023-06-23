1. Make sure `osmium` is installed
2. Download the region as `.osm.pbf` file of your choice (for example https://download.geofabrik.de/europe/germany-latest.osm.pbf)
3. Extract data with highway tags: `osmium tags-filter -o data.osm.pbf germany-latest.osm.pbf w/highway=motorway,trunk,primary,secondary,tertiary,unclassified,residential,motorway_link,trunk_link,primary_link,secondary_link,tertiary_link,living_street,service,track,road --overwrite`
    * This step might take a while, depending on the dataset size (for Germany around 30s)
4. Convert to OPL file: `osmium cat data.osm.pbf -f opl -o data.opl --overwrite`
5. Analyze the file: `python node-degree.py data.opl`
    * Use `python node-degree.py data.opl opl` to create additional OPL output of all nodes taken into account
