#!/bin/python

import os
import sys
import glob
import seaborn as sns
import pandas as pd

if len(sys.argv) - 1 != 2:
	print("ERROR: Wrong number of arguments: Expected 2, found %s" % len(sys.argv))
	print("Expexted parameters: {glob-pattern} {title}")
	sys.exit(1)

#dataset_filter = "../results/pattern-based-rectangles/pattern_*x*_performance_"+title+".csv"
dataset_filter = sys.argv[1]
title = sys.argv[2]

dataset_files = glob.glob(dataset_filter)
dataset_container = []
for file in dataset_files:
    df = pd.read_csv(file)
    dataset_container.append(df)

dataset = pd.concat(dataset_container)

sns.set(rc={
	"figure.figsize": (5, 4),
	"figure.dpi": 100,
})

sns.set_style("whitegrid", {
	"font.sans-serif": ["Droid Sans"]
})

#plot = sns.boxplot(
#	data=dataset,
#	x="total_vertices",
#	y="iteration_time",
#	whis=10,
#)
#figure = plot.get_figure()

plot = sns.lineplot(
	data=dataset,
	x="total_vertices",
	y="iteration_time",
	marker='o',
	markersize=5,
	err_style="bars",
	errorbar=lambda x: (x.min(), x.max()),
	linewidth=1,
	err_kws={"elinewidth": 1},
	zorder=10,
	clip_on=False,
)
plot.set_xlim(0, None)
plot.set_ylim(0, None)
plot.set_title(title, pad=15, fontsize=14)
plot.set_xlabel("Input vertices", labelpad=10, fontsize=9)
plot.set_ylabel("Constructor time in ms", labelpad=10, fontsize=9)
plot.tick_params(axis="both", which="major", labelsize=9)
#plot.set(yscale="log")
figure = plot.get_figure()

figure.tight_layout()
figure.savefig(
	"./"+os.path.basename(__file__)+".png",
	#pad_inches=0,
	#bbox_inches='tight',
)
