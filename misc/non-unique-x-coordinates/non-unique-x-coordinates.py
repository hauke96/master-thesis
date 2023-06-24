#!/usr/bin/env python

import sys

x_to_nodes = dict()
node_to_x = dict()
all_segments = list()

with open(sys.argv[1], "r") as file:
	for line in file:
		line = line.strip()
		segments = line.split(" ")
		all_segments.append(segments)

		# For nodes:
		if segments[0][0] == "n":
			for i in range(len(segments)):
				if segments[i][0] == "x":
					node_to_x[segments[0]] = segments[i]
					if segments[i] not in x_to_nodes:
						x_to_nodes[segments[i]] = list()
					x_to_nodes[segments[i]].append(segments[0])

# Create OPL output with all nodes taken into account
if len(sys.argv) > 2 and sys.argv[2] == "opl":
	for segments in all_segments:
		# For nodes:
		if segments[0][0] == "n":
			has_uniq_x = True
			for i in range(len(segments)):
				# remove all tags (saves space)
				if segments[i][0] == "T":
					segments[i] = "T"

				if segments[i][0] == "x":
					if x_to_nodes[segments[i]] > 1:
						print(" ".join(segments))
	print()
	print()

non_uniq_nodes = dict((k,v) for k,v in x_to_nodes.items() if len(v) > 1)

for k,v in non_uniq_nodes.items():
	print(k + " : " + " ".join(v))
print()
print("Total number of nodes:")
print("  "+str(len(node_to_x)))
print("Number of non-unique x-coordinates:")
print("  "+str(len(non_uniq_nodes)))
print("Percent of nodes with non-unique x-coord:")
print("  "+str(len(non_uniq_nodes)/len(node_to_x)*100)+"%")
print()
print("x-coord occurrence histogram:")
x_to_occurrences = dict((k,len(v)) for k,v in non_uniq_nodes.items())
for i in range(max(x_to_occurrences.values())):
	s = sum(1 for k in x_to_occurrences.values() if k == i)
	print("  "+str(i)+"="+str(s))

