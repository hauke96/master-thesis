#!/bin/python

import os
import sys
import glob
import seaborn as sns
import pandas as pd
import matplotlib.lines as mlines
from matplotlib.colors import ListedColormap
import matplotlib.cm as cm

color_palette_flare=sns.color_palette("flare", as_cmap=True)
color_palette_blue=sns.color_palette("blend:#2779b4", as_cmap=True)
color_palette_blue_green=sns.color_palette("blend:#2779b4,#27b43e", as_cmap=True)
color_palette_blue_red=sns.color_palette("blend:#2779b4,#b42727", as_cmap=True)
color_palette_selected_name="custom_blue"

cm.register_cmap("custom_flare", color_palette_flare)
cm.register_cmap("custom_blue", color_palette_blue)
cm.register_cmap("custom_blue-green", color_palette_blue_green)
cm.register_cmap("custom_blue-red", color_palette_blue_red)

fontsize_small=9
fontsize_large=14

errorbars_minmax=lambda x: (x.nanmin(), x.nanmax())

def check_args(expected_number):
	if len(sys.argv) - 1 != expected_number:
		print("ERROR: Wrong number of arguments: Expected %s, found %s" % (expected_number, len(sys.argv)))
		print("  > %s" % sys.argv[1:])
		print("Expexted parameters: {glob-pattern} {title}")
		sys.exit(1)

def load_dataset(dataset_filter, title):
	dataset_files=glob.glob(dataset_filter)
	dataset_container=[]
	for file in dataset_files:
	    df=pd.read_csv(file)
	    dataset_container.append(df)
	
	dataset=pd.concat(dataset_container)
	return dataset

def init_seaborn(
		width=5,
		height=4,
		dpi=100,
		palette='colorblind',
	):

	global color_palette_selected_name

	sns.set(rc={
		"figure.figsize": (width, height),
		"figure.dpi": dpi,
	})
	
	sns.set_style("whitegrid", {
		"font.sans-serif": ["Droid Sans"]
	})

	sns.set_palette(palette)
	color_palette_selected_name=palette

#plot=sns.boxplot(
#	data=dataset,
#	x="total_vertices",
#	y="iteration_time",
#	whis=10,
#)
#figure=plot.get_figure()

def create_lineplot(
		dataset,
		title="",
		xcol="total_vertices",
		ycol="iteration_time",
		xlabel="Input vertices",
		ylabel="Time in ms",
		hue=None,
		style=None,
		errorbar=None,
		yscale=None,
		ax=None,
		color=None,
		marker="o",
	):

	err_style="bars" if errorbar != None else None
	err_kws={"elinewidth": 1} if errorbar != None else None

	plot=sns.lineplot(
		data=dataset,
		x=xcol,
		y=ycol,
		hue=hue,
		style=style,
		color=color,
		palette=color_palette_selected_name,
		marker=marker,
		markersize=5,
		err_style=err_style,
		errorbar=errorbar,
		err_kws=err_kws,
		linewidth=1,
		zorder=10,
		clip_on=False,
		ax=ax
	)

	if yscale == "log":
		plot.set(yscale="log")
	else:
		plot.set_xlim(0, None)
		plot.set_ylim(0, None)

	plot.set_title(title, pad=12, fontsize=fontsize_large)
	plot.set_xlabel(xlabel, labelpad=5, fontsize=fontsize_small)
	plot.set_ylabel(ylabel, labelpad=5, fontsize=fontsize_small)
	plot.tick_params(axis="both", which="major", labelsize=fontsize_small)

	return plot

def create_scatterplot(
		dataset,
		title="",
		xcol="",
		ycol="",
		xlabel="",
		ylabel="",
		hue=None,
		style=None,
		yscale=None,
		ax=None,
		color=None,
		marker="x",
	):

	plot=sns.scatterplot(
		data=dataset,
		x=xcol,
		y=ycol,
		hue=hue,
		style=style,
		palette=color_palette_selected_name,
		marker=marker,
		edgecolor=None,
		color=color,
		linewidth=1,
		s=10,
		legend = False,
		zorder=10,
		clip_on=False,
		ax=ax
	)

	if yscale == "log":
		plot.set(yscale="log")
	else:
		plot.set_xlim(0, None)
		plot.set_ylim(0, None)

	plot.set_title(title, pad=12, fontsize=fontsize_large)
	plot.set_xlabel(xlabel, labelpad=5, fontsize=fontsize_small)
	plot.set_ylabel(ylabel, labelpad=5, fontsize=fontsize_small)
	plot.tick_params(axis="both", which="major", labelsize=fontsize_small)

	return plot

'''
This function selects the legend labels for the given column names
"col_values" and colors them according to color_palette_selected_name.
'''
def set_numeric_legend(plot, title, col_values):
	col_values=col_values.unique()
	col_values.sort()
	col_values=[str(v) for v in col_values]

	handles, labels=plot.get_legend_handles_labels()

	# Generate as many color values as we need according to currently selected
	# colormap
	colors=sns.color_palette(palette=color_palette_selected_name, n_colors=len(col_values))

	# Find out at which index the style items for each col_value start
	col_value_start_index=len(labels) - 1 - labels[::-1].index(col_values[0])

	# Select only the style items for our col_values
	labels=labels[col_value_start_index:]
	handles=handles[col_value_start_index:]

	# Set color to each handle
	for i in range(len(handles)):
		handles[i].set_color(colors[i])

	plot.legend(
		handles=handles,
		labels=labels,
		title=title,
		title_fontsize=fontsize_small,
		fontsize=fontsize_small,
		loc="center left",
		bbox_to_anchor=(1.025, 0.5),
		#borderaxespad=0,
	)

def save_to_file(
		figure,
		filename,
		extension="png",
		no_margin=False
	):

	figure.tight_layout()
	figure.savefig(
		"./"+filename+"."+extension,
		pad_inches=0 if no_margin else None,
		bbox_inches='tight' if no_margin else None,
	)
