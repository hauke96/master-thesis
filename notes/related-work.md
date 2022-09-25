This is a list of related work for my thesis ordered by topic. A summary means that I (roughly) looked into the work and checked that it's somehow related to my topic. The amount of quotations comes from Google Scholar and is just used as a rule of thumb measurement for the importance of the paper.

# Routing

## Shortest paths among obstacles in the plane (Mitchell, 1993)

Quoted by 236.

* https://dl.acm.org/doi/abs/10.1145/160985.161156

### Summary

To find shortest paths in the open space, Mitchell proposed an sub-quadratic algorithm using wavefronts traveling through space instead of a visibility graph. Each wavefront (or more precise "wavelet") can collide with vertices, edges and other wavelets. It then is split or reduces in size and new wavelets are spawned on vertices. The output of his algorithm is a shortest path map with which such a path can be found in O(log n).

## Efficient algorithms for Euclidean shortest path and visibility problems with polygonal obstacles (Kapoor, Maheshwari, 1988)

They talk about building a visibility graph → is this state of the art?

* https://dl.acm.org/doi/abs/10.1145/73393.73411

## An Optimal Algorithm for Euclidean Shortest Paths in the Plane (Hershberger, Suri, 1999)

Quoted by 383.

* https://www.semanticscholar.org/paper/An-Optimal-Algorithm-for-Euclidean-Shortest-Paths-Hershberger-Suri/4c1f2cc5262541db0f1047dab0f0789968f8975d

### Summary

This paper is about a time-optimal algorithm for shortest paths in the plane (→ continuous dijkstra). It's an optimization of the wavefront algorithm by using a smart quad-tree structure for the vertices and by using approximate wavefronts.
The result of the algorithm is a shortest path map, where for every location the shortest path can easily (→ O(log n) with n = number of vertices) be determined. The computation of the map itself takes O(n log n) time.

## A Navigation Algorithm for Pedestrian Simulation in Dynamic Environments (Teknomo, Millonig, 2007)

Quoted by 27.

// TODO Look further into it, sounds interesting

* https://www.semanticscholar.org/paper/A-Navigation-Algorithm-for-Pedestrian-Simulation-in-Teknomo-Millonig/4f2aa5e89b5d10897abd77b70ebf2f8435950ee9

## Automatic extrapolation of missing road network data in OpenStreetMap (Funke, Schirrmeister, Storandt, 2015)

Quoted by 19.

They explicitly do not use Mitchells algorithm (because it's too expensive) but use a grid and remove all point and edges that are within an obstacle. Then they compare the length of the path used by OSM routing and routing on that remaining grid.

* http://ceur-ws.org/Vol-1392/paper-04.pdf

## Optimal pedestrian path planning in evacuation scenario (Kasanicky, Zelenka, 2014)

* http://147.213.75.17/ojs/index.php/cai/article/view/2814/675

# Agent-based simulations

## Introductory tutorial: Agent-based modeling and simulation (Macal, North, 2014)

Quoted by 380.

* https://ieeexplore.ieee.org/document/7019874
* http://simulation.su/uploads/files/default/2014-macal-north.pdf
* Different paper by Macal but maybe also interesting: https://www.researchgate.net/profile/C-Macal/publication/302923196_Everything_you_need_to_know_about_agent-based_modelling_and_simulation/links/61bbd2d44b318a6970e8d794/Everything-you-need-to-know-about-agent-based-modelling-and-simulation.pdf (Quoted by 385)

### Summary

This document describes the basics of agent-based simulation. It presents the different definitions, use-cases, states examples, goes through each aspect of an agent, gives best-practice tips on how to design a agent-based model and answers the question why and when agent-based models should be used. It also links to about 60-70 other papers of this topic.

## Agent-based evacuation simulation from subway train and platform (Zou, Fernandes, Chen, 2019)

Quoted by 12.

* https://www.researchgate.net/publication/334094675_Agent-based_evacuation_simulation_from_subway_train_and_platform
* https://www.researchgate.net/profile/Qiling-Zou/publication/334094675_Agent-based_evacuation_simulation_from_subway_train_and_platform/links/5db0695492851c577eb9e293/Agent-based-evacuation-simulation-from-subway-train-and-platform.pdf

## A hybrid simulation model of passenger emergency evacuation under disruption scenarios: A case study of a large transfer railway station (Hassannayebi, et al., 2018)

Quoted by 21.

* https://www.tandfonline.com/doi/abs/10.1080/17477778.2019.1664267

## Modelling building emergency evacuation plans considering the dynamic behaviour of pedestrians using agent-based simulation (Rozo, et al., 2019)

Quoted by 74.

* https://www.sciencedirect.com/science/article/abs/pii/S0925753518300560

## Multi agent simulation of pedestrian behavior in closed spatial environments (Camillen, Capri, et al., 2009)

Quoted by 41.

* https://ieeexplore.ieee.org/abstract/document/5444471

## A Dynamic Multiagent Simulation of Mobility in a Train Station (Boulet, Zargayaouna, Leurent, et al., 2018)

Quoted by 6.

* https://hal.archives-ouvertes.fr/hal-01689573/document

## Agent-Based Pedestrian Simulation (Rindsfüser, Klügl, 2007)

Quoted by 28.

* https://www.researchgate.net/publication/271750003_Agent-Based_Pedestrian_Simulation

## Multimodel agent-based simulation environment for mass-gatherings and pedestrian dynamics (Karbovskii, et al., 2016)

Quoted by 24.

* https://www.sciencedirect.com/science/article/abs/pii/S0167739X16303739
