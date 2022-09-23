This is a list of possible scenarios.
Each scenarios has to fulfill a set of criteria to be a good scenario.

# Criteria

1. **Scenario must fit to geometric routing.** The situations must handle with open areas or places with bad/few lines for a routing network.
2. **Expect different results.** Simulations using network and geometric routing are expected to lead to different, rather than same results. This is interesting for the evaluation where I want to check exactly that: Does the geometric routing help to create a more accurate simulation of the reality?
3. **Heterogeneity of agents.** Agents are all slightly different, a good scenario makes use of that.
4. **Comprehensibility.** The scenario should be easily understandable, people should be able to relate to it.
5. **Significance.** The scenario should deal with a significant topic (like evacuation of area).

# Scenarios

## Flooded district

### Idea

A district of Hamburg (e.g. Veddel) is hit by a storm flood, some roads are inaccessible and routing through green areas, company ground and other free spaces is necessary for evacuation and traveling of agents. Pure network based routing might be limited in this scenario where some roads and ways are not usable.

### Behavior of agents

Agents move freely and independent through space. Motivation: Legal restrictions (e.g. illegal entering of private property) is unimportant in an emergency situation → movement differs from normal situations.

### Scientific question

### Usefulness of geometric routing

When roads sections are unavailable for network based routing, geometric routing can fill this gap and enable agents to still move towards a target location. This improves the quality of a simulation as it becomes more realistic.

### Complexity of development/modeling

1. Data about flooding needed. Where is the district flooded at certain water levels? Where does the data come from?
2. Algorithm needs to be extended to work with dynamic changes in unavailable areas. It must be possible to dynamically add and remove obstacles. However, the current implementation is tied to the preprocessed visibility graph.

### Evaluation

* Comparison to network based routing possible

### Risks and open questions

* Area of interest is rather large, performance might become an issue?

## HH central station

### Idea

The central station got new stairs at the southern end of the platforms (e.g. this one https://www.openstreetmap.org/way/963178651). This might influence movement of agents which will be analyzed in this scenario.

One can think of several variants of this scenario:

* Evacuation: Do the new stairs help to evacuate a platform (or the whole station) more quickly?
* Changing between train and (local) public transport: Are the stairs helping to reach subway, busses and other trains more quickly? These cases might itself be sub-variants where agents just have different target positions.

### Behavior of agents

Each agent leaves or enters the train at a certain position (→ at door positions from the cars of a train). 

### Scientific question

* Do the new additional stair help? If so, in what situations and in what do they not help?
  * Why are the stair in some situations (or for some agents) not helping?
* Can even more stair improve the situation further? E.g. additional stair in the middle of the station or at the northern end.
* Do different trains have significantly different impact? At some platforms two trains are stopping behind each other, so one is within the station and the second one on the same track is further south.
* Are there big train stations with a good situation for travelers (e.g. terminus stations like Frankfurt). Would a transformation of Hamburg towards a terminus station help?

### Usefulness of geometric routing

Even though train stations have few large open spaces, the platforms are still areas as well as stair and the corridors above and below the station. Using a network limits the movement of agents and movement of agents with in a mass of people can only be roughly approximated.

### Complexity of development/modeling

* The OSM data of the central station can be used even though is needs to be tweaked: Stair must be areas as they are in real life, missing barriers must be added and other obstacles on the platforms need to be added too.
* The routing algorithm must be extended to work with multiple vertical levels of movement. Agents must work with that as well (e.g. collision of agents on different levels must be deactivated, etc.)
* The number and target location of each agent must be approximated or we find exact data on how many people are walking within the station from which to which location.
* A limited number of source and target locations might help to keep things simple and enables us to spawn agents accurately (e.g. every five minutes x many agents spawn in a time span of one minute at that subway exit).

### Evaluation

* Comparison using network based routing possible but ways must probably be added to support this.
* Concrete changes of scenario measurable (e.g. add/remove stairs and see what happens)

### Risks and open questions

* How difficult is the routing with multiple levels?

## Demonstration within the City

// TODO

### Idea

### Behavior of agents

### Scientific question

### Usefulness of geometric routing

### Complexity of development/modeling

### Evaluation

### Risks and open questions
