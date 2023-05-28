This folder contains some python scripts to generate visualizations of the measured data from `../results` using seaborn.

# Scripts

* `plot-all-for-pattern.sh`: Generates all images. Takes on argument, which is the name of the result folder (e.g. "pattern-based-rectangles").
* `common.py`: This file contains shared code for the different rendering scripts.
* `plot-iteration-time-per-vertices.py`: Plots the import time of multiple datasets relative to the number of input vertices.
* `plot-routing-memory.py`: Plots the memory usage for routing requests of multiple datasets. This produces multiple colored plots on top of each other.
* `plot-routing-time-per-length.py`: Plots the time per meter needed for a routing request based on the number of input vertices.
* `plot-routing-time.py`: Plots the time needed for routing requests of multiple datasets. This produces multiple colored plots on top of each other.

Usage example can be found in each script file.
