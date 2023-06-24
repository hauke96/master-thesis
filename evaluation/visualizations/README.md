This folder contains some python scripts to generate visualizations of the measured data from `../results` using seaborn.

# Scripts

* `common.py`: This file contains shared code for the different rendering scripts.
* `plot-iteration-details-per-vertices.py`: Plots detailed import times of the given dataset.
* `plot-iteration-time-per-vertices.py`: Plots the import time of multiple datasets relative to the number of input vertices.
* `plot-routing-length-factor.py`: Plots the ratio between real and beeline distance. The real route distance is always longer than the beeline distance by a factor of f>=1.
* `plot-routing-memory.py`: Plots the memory usage for routing requests of multiple datasets. This produces multiple colored plots on top of each other.
* `plot-routing-time.py`: Plots the time needed for routing requests of multiple datasets. This produces multiple colored plots on top of each other.
* `plot-routing-time-per-length.py`: Plots the time per meter needed for a routing request based on the number of input vertices.
* `plot-routing-time-details.py`: Plots detailed routing times of the given dataset.

Usage example can be found in each script file.

The `plot-all-for-pattern.sh` script generates all images. Takes on argument, which is the name of the result folder (e.g. "pattern-based-rectangles").
