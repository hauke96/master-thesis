#!/usr/bin/env python3

import sys
import pandas as pd
import glob

'''
Gets the average, minimum and maximum values for each given dataset and writes them into a new CSV file.
'''

if len(sys.argv) - 1 != 2:
	print("Wrong number of arguments: Expected 2, found %s" % (len(sys.argv) - 1))
	print("Expected parameters:")
	print("  1. Folder with CSV datasets")
	print("  2. Filter matching the wanted files. E.g. \"*_CalculateRoute.csv\"")
	sys.exit(1)

csvFilePaths = glob.glob(sys.argv[1] + "/" + sys.argv[2])

outputData = {}
outputData['file'] = []
outputData['input_vertices'] = []
outputData['avg_time'] = []
outputData['min_time'] = []
outputData['max_time'] = []

print("Read CSV files and collect data")

for csvFilePath in csvFilePaths:
    content = pd.read_csv(csvFilePath)
    data = content.loc[content['iteration_number'] == 0]

    outputData['file'].append(csvFilePath)
    outputData['input_vertices'].append(data['input_vertices'].values[0])
    outputData['avg_time'].append(data['avg_time'].values[0])
    outputData['min_time'].append(data['min_time'].values[0])
    outputData['max_time'].append(data['max_time'].values[0])

print("Data consolidated, create CSV file")

outputDataFrame = pd.DataFrame(outputData)
outputDataFrame = outputDataFrame.sort_values(by=['input_vertices'])
outputDataFrame.to_csv("./consolidate-avg-min-max-data.csv", index=False)

print("Done")
