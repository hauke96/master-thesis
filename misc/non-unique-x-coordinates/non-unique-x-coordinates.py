#!/usr/bin/env python

import sys
import osmium

class MyHandler(osmium.SimpleHandler):
	def __init__(self):
		osmium.SimpleHandler.__init__(self)
		self.x_to_nodes = dict()
		self.node_to_x = dict()

	def node(self, n):
		lon = n.location.lon
		self.node_to_x[n] = lon
		if lon not in self.x_to_nodes:
			self.x_to_nodes[lon] = list()
		self.x_to_nodes[lon].append(str(n.id))

handler = MyHandler()
handler.apply_file(sys.argv[1])

non_uniq_nodes = dict((k,v) for k,v in handler.x_to_nodes.items() if len(v) > 1)

for k,v in non_uniq_nodes.items():
	print(str(k) + " : " + " ".join(v))
print()
print("Total number of nodes:")
print("  "+str(len(handler.node_to_x)))
print("Number of non-unique x-coordinates:")
print("  "+str(len(non_uniq_nodes)))
print("Percent of nodes with non-unique x-coord:")
print("  "+str(len(non_uniq_nodes)/len(handler.node_to_x)*100)+"%")
print()
print("x-coord occurrence histogram:")
x_to_occurrences = dict((k,len(v)) for k,v in non_uniq_nodes.items())
for i in range(max(x_to_occurrences.values())):
	s = sum(1 for k in x_to_occurrences.values() if k == i)
	print("  "+str(i)+"="+str(s))

