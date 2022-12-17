# Render performance measurements

Use the script `consolidate-avg-min-max-data.py` so merge the average, minimum and maximum time of certain datasets to a single CSV file.
Such a CSV file can then be rendered using the gnuplot file.

1. Merge data from several CSV files: `./consolidate-avg-min-max-data.py pattern-based/ "*_WavefrontAlgorithmCreation.csv"` (use other filter to specify what CSV you want)
2. Use `gnuplot consolidate-avg-min-max-data.gnuplot` to render the data

## CSV file format

The CSV file must have the following columns in the given order (the python script mentioned above produces exactly this).
The gnuplot file requires this and references the data by their position.
The file should also be sorted by `input_vertices` to draw correct lines.

Columns: `file,input_vertices,avg_time,min_time,max_time`
