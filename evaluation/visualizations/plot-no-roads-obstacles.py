#!/bin/python

'''
Plots all necessary metrics of the "no-road" and "no-obstacles" variant of a given dataset.

Parameters: {result-folder}

Example: ./plot-no-roads-obstacles.py "../results/osm-based-city"
'''

import common
import os
import sys
import pandas as pd
import seaborn as sns
import matplotlib.pyplot as plt
from matplotlib import container

common.check_args(1)

result_folder=sys.argv[1]

dataset_cols=[
	# Only when yscale='log':
	'iteration_time',
	'knn_search_time',
	'build_graph_time',
	'get_obstacle_time',
	'merge_road_graph_time',
	'add_poi_attributes_time',
]
dataset_labels=[
	# Only when yscale='log':
	'Total time',
	'kNN search',
	'Create graph',
	'Get obstacles',
	'Merge road edges',
	'Add POI attributes',
]

common.init_seaborn(format="large_slim", palette='muted')

dataset_normal=common.load_dataset(result_folder + "/4km2_performance_GenerateGraph.csv")
dataset_no_roads=common.load_dataset(result_folder + "-no-roads/data_performance_GenerateGraph.csv")
dataset_no_obstacles=common.load_dataset(result_folder + "-no-obstacles/data_performance_GenerateGraph.csv")

dataset=pd.concat([
    dataset_normal.assign(dataset="normal"),
    dataset_no_roads.assign(dataset="no-roads"),
    dataset_no_obstacles.assign(dataset="no-obstacles")
])
dataset=dataset[dataset_cols + ["dataset"]]
dataset=dataset.melt(id_vars=["dataset"], var_name='aspect', value_name='time')
dataset["time"]=dataset["time"] / 1000

#
# Import time vs. normal dataset
#
fig, ax = plt.subplots()

common.create_barplot(
    dataset,
    xcol="aspect",
    ycol='time',
    ylabel='Time in s',
    hue="dataset",
    yscale='log',
    ax=ax
)

handles, labels = ax.get_legend_handles_labels()
handles=[h for h in handles if not isinstance(h, container.ErrorbarContainer)]

sns.move_legend(
    ax,
    "center left",
    bbox_to_anchor=(1.025, 0.5),
    handles=handles,
    labels=["Normal", "No roads", "No obstacles"],
    title_fontsize=common.fontsize_small,
    fontsize=common.fontsize_small,
    title='Dataset'
)

#ax.legend([],[], frameon=False)
ax.set_xticklabels(common.wrap_labels(dataset_labels))

common.save_to_file(fig, os.path.basename(__file__) + "_absolute")
