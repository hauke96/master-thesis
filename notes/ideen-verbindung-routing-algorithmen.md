# Verbindung Routing Srategien

tl;dr "Merge der Netze" wird weiter verfolgt.

## Absprung an bestimmten Stellen

An bestimmten Stellen (allen Knoten, Kreuzungen, alle x Meter, etc.) wird mit A*/Dijkstra temporär aufgehört und mit geometrischem Routing versucht weiter zu kommen. Die entstandenen Pfade werden in das Netz von A*/Dijkstra eingebunden. Dann setzt man das graph-routing fort.

### Vorteile

* Man erreicht tatsächlich Start und Endpunkte (end-to-end routing)

### Fragen

* Wie wählt man die Absprungstellen aus?
* Wie lange routet man geometrisch?
* Kann man einfach das Netz erweitern?
* Wie wirkt sich das auf die bereits ermittelten Kosten von Kanten/Pfaden aus? Bereits ermittelte Pfade haben Gewichte, Vorgänger, etc. aber die neuen Kanten nicht. Diese bilden aber ggf. Abkürzungen und neue kürzeste Pfade, die erst mal erkannt werden müssen.
* Funktioniert das überhaupt? Vielleicht werden dann einfach die neuen Kanten zunächst verfolgt, da diese bisher nicht betrachtet wurden und dadurch kürzere Pfade liefern, als die bisherigen Pfade.

### Nachteile

* Ungewiss, ob es funktioniert
* Sehr außergewöhnliche Strategie
* Wahrscheinlich sehr aufwändig

## Konkurrierendes Routing

Man lässt beide Routing Varianten los, hat also am Ende zwei Routen. Man kann nun einfach die kürzere von beiden nehmen, oder: Man schneidet die Routen an den Stellen, wo sie sich kreuzen und baut aus den Teilstücken eine kürzeste Route zusammen.

### Vorteile

* Simpley vorgehen
* Technisch einfach umzusetzen

### Nachteile

* Das Netz-Routing kann ggf. nicht exakt von Start zu Ende routen. Der Vergleich ist daher per se schon ungenau/nicht möglich
* Ungewiss, ob es funktioniert: Vielleicht schneiden sich die Routen nur selten oder fast nie?
* Keine genaue Priorisierung/Gewichtung der Kanten möglich, auf jedem Teilstück ist das eine entweder-oder-Entscheidung

## Konkurrierendes Routing auf Teilstrecken

Man berechnet die Routen konkurrierend auf Teilstücken. Diese können z.B. ermittelt werden, indem man die Route auf einem Netz ermittelt, dann alle x Meter einen Punkt bestimmt und zwischen allen Punkten der Route geometrisch routet. Ist das geometrisch geroutete Teistück um das y-fache kürzer als die Netz-Strecke, wird der Netz-Teilabschnitt durch den geometrisch gerouteten Teilabschnitt ersetzt.

### Vorteile

* Genauer als obige Konkurrenz-Methode
* Immer noch verhältnismäßig einfach

### Nachteile

* Das Netz-Routing kann ggf. nicht exakt von Start zu Ende routen. Der Vergleich ist daher an den Start- und End-Teilstrecken ungenau/nicht möglich
* Ggf. ungenau: Wahl der Punkte könnte Routing-Ergebnis erheblich beeinflussen. Was ist, wenn von A nach B eine nicht so gute geometrische Route erstellt wird, von der Mitte zwischen A/B zur Mitte von B/C aber eine sehr gute?

## Merge der Netze

Der Visibility-Graph (oder ein anderes erzeugtes Netz kürzester Pfade?) wird mit dem normalen Netz gemerged. Danach benutzt man einfach A*/Dijkstra zum routen.

### Vorteile

* Einfache Strategie und Umsetzung
* Routen werden definitiv die kürzesten sein, da A*/Dijstra ja nun mal funktioniert
* Start und Endpunkte können erreicht werden, wenn man diese vorher in den Visibility-Graph integriert (was mein bisheriges Preprocessing aber schon macht)

### Nachteile

* Ggf. schlechte performance durch zu viele Kanten (→ Methoden zum Ausdünnen nutzen)

# Merge der Netze

## Probleme

### Einfache Nachbarschaftsbeziehungen

Angenommen ein Knoten v auf einem LineString (z.B. ein Zaun) hat Nachbarn u und w auf beiden Seiten, sprich u und w sehen sich nicht, v sieht aber beide.
Nimmt man einfach diese Nachbarschaften als Kanten für einen Graphen, würde A* einfach durch das Hindernis durch routen, da eine Verbindung von u zu v zu w besteht.

Skizze (x, y, z sind fest mit v verbunden, u und w sind teil anderer Hindernisse aber sichtbar von v aus):
```
     x   z
     | /
u -- v -- w
     |
     y
```

#### Lösung A: Mapping der Nachbarschaften anpassen

Derzeit wird ein Mapping `<Vertex, List<Vertex>>` ermittelt, aber `<Vertex, List<List<Vertex>>>` wäre vielleicht besser.
Jede Liste `List<Vertex>` enthält nur noch Knoten die gegenseitig über den key-Vertex erreichbar sind.
Die kann man ermitteln indem zwischen allen direkten Nachbarn (x, y, z) die sichtbaren Knoten ermittelt.

Im obigen Beispiel wären das also drei Listen:

* x, y, u
* z, y, w
* x, z

**Offene Fragen:**

* Klappt es, dass man im Graphen für A* dann mehrere Knoten an der identischen Stelle hat? Es müsste nämlich an der Position von v für alle drei Nachbarschaftslisten einen Knoten geben.

**Vorteile:**

* Performance von Routing-Requests nicht beeinträchtigt

**Nachteile:**

* Aufwändiger als Lösung B (aber nicht großartig komplex)
* Performance vom Preprocessing langsamer
* Leichte Ungewissheit wegen der offenen Frage oben

#### Lösung B: Ad-hoc Routing der Wavefronts

Wenn für das hybride Verfahren ein Request rein kommt läuft das so:

1. In Wavefront Algorithmus den Zielknoten einfügen
2. Algorithmus laufen lassen, bis Zielknoten erreicht wurde
3. Alle ermittelten Pfade in A*-Graph einpflanzen
4. A* laufen lassen

**Vorteile:**

* Sehr einfach umzusetzen, da wir bereits alle kürzesten Pfade bekommen

**Nachteile:**

* Performance der Routing-Requests langsamer
