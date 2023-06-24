# Usage

1. Make sure `osmium` is installed
2. Download the region as `.osm.pbf` file of your choice (for example https://download.geofabrik.de/europe/germany-latest.osm.pbf)
3. Extract data obstacle data: `osmium tags-filter -o data.osm.pbf hamburg-latest.osm.pbf w/building w/barrier w/railway w/natural --overwrite`
    * This step might take a while, depending on the dataset size
4. Analyze the file: `python non-unique-x-coordinates.py data.osm.pbf`

# Results

For Hamburg (2023-06-04) *without* any filtering:

```
Total number of nodes:
  3268956
Number of non-unique x-coordinates:
  745188
Percent of nodes with non-unique x-coord:
  22.795901810853373%

x-coord occurrence histogram:
  0=0
  1=0
  2=536410
  3=157799
  4=39815
  5=8826
  6=1813
  7=391
  8=96
  9=23
  10=11
  11=1
  12=0
  13=2
```
