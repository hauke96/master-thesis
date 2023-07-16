#!/bin/python

'''
Plots the hausdorff distances as bars for the given dataset.
The dataset files must be in this folder and must contain the necessary suffixes (s. code below).

Parameters: {dataset-prefix}

Example: ./plot.py osm-city
'''

import os
import sys
import pandas as pd
import seaborn as sns
import matplotlib.pyplot as plt
from matplotlib import container

# Import stuff from super-dir
sys.path.append('../')
import common

common.init_seaborn(format="large_slim", palette="muted")

dataset_cat=sys.argv[1] # e.g. "osm-city"

#
# Hausdorff distances
#

dataset_hiker=common.load_dataset(dataset_cat+"-hiker-hausdorff_distances.csv")
dataset_hiker=dataset_hiker[dataset_hiker["id"]<=10]
dataset_hiker["sort_key"]=dataset_hiker["id"]
avg = dataset_hiker.mean()
avg["id"]="mean"
avg["sort_key"]=999
dataset_hiker.loc["mean"] = avg
dataset_hiker["source"]="hiker"
dataset_hiker=dataset_hiker.sort_values(by=['sort_key'])

dataset_routing=common.load_dataset(dataset_cat+"-routing-hausdorff_distances.csv")
dataset_routing=dataset_routing[dataset_routing["id"]<=10]
dataset_routing["sort_key"]=dataset_routing["id"]
avg = dataset_routing.mean()
avg["id"]="mean"
avg["sort_key"]=999
dataset_routing.loc["mean"] = avg
dataset_routing["source"]="routing"
dataset_routing=dataset_routing.sort_values(by=['sort_key'])

dataset=pd.concat([dataset_hiker, dataset_routing])

fig, ax = plt.subplots()
common.create_barplot(
    dataset,
    xcol="id",
	xlabel="Routing request",
    ycol='h_dist',
    ylabel='Hausdorff distance in m',
    hue="source",
    ax=ax
)

handles, labels = ax.get_legend_handles_labels()
handles=[h for h in handles if not isinstance(h, container.ErrorbarContainer)]

sns.move_legend(
    ax,
    "center left",
    bbox_to_anchor=(1.025, 0.5),
    handles=handles,
    labels=["Hybrid\nrouting\nalgorithm", "Graph-\nbased\nrouting"],
    title_fontsize=common.fontsize_small,
    fontsize=common.fontsize_small,
    title=None
)

common.save_to_file(fig, os.path.basename(__file__) + "_" + dataset_cat + "_hausdorff")

#
# Relative route distances (= comparison to beeline distance)
#

def set_mean(dataset, source):
    mean = dataset[dataset["source"] == source]
    mean["source"] = 0
    mean = mean.mean()
    mean["id"] = "mean"
    mean["sort_key"] = 999
    mean["source"] = source
    dataset.loc[source] = mean

dataset = common.load_dataset(dataset_cat+"-distances.csv")
dataset["sort_key"]=pd.to_numeric(dataset["id"])

set_mean(dataset, "hiker")
set_mean(dataset, "routing")
set_mean(dataset, "expected")

dataset=dataset.sort_values(by=['sort_key'])

[p1, p2, p3]=sns.color_palette("muted", 3)
palette=[p3, p1, p2]
common.init_seaborn(format="large_slim", palette=palette)

fig, ax = plt.subplots()
common.create_barplot(
    dataset,
    xcol="id",
	xlabel="Routing request",
    ycol='distance_relative',
    ylabel='Route distance /\nbeeline distance',
    hue="source",
    ax=ax
)

handles, labels = ax.get_legend_handles_labels()
handles=[h for h in handles if not isinstance(h, container.ErrorbarContainer)]

sns.move_legend(
    ax,
    "center left",
    bbox_to_anchor=(1.025, 0.47),
    handles=handles,
    labels=["Expected\nroute", "Hybrid\nrouting\nalgorithm", "Graph-\nbased\nrouting"],
    title_fontsize=common.fontsize_small,
    fontsize=common.fontsize_small,
    title=None
)

ax.set_ylim(0.9, None)

common.save_to_file(fig, os.path.basename(__file__) + "_" + dataset_cat + "_relative-beeline")