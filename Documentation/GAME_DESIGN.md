# Seabright game design

**Version 0.2.1 · original coastal city-building demo**

Seabright explores whether an AI-assisted development process can deliver a coherent simulation game: understandable city growth, distinct architecture, a usable interface, audible feedback and evidence from actual play. Its playable scope is a small settlement that becomes a serviced modern district with towers and a stadium. The project prioritizes that complete progression over a large catalogue of disconnected features.

The planning reference is the outside connection → zoning → services → development → budget loop described in the [official Cities: Skylines overview](https://www.paradoxinteractive.com/games/cities-skylines/about) and [user manual](https://cdn.akamai.steamstatic.com/steam/apps/255710/manuals/CitiesSkylines-UserManual_EN.pdf). Seabright uses original code, interface, landmarks and music alongside separately licensed reusable assets. No Cities: Skylines assets or recordings were extracted. This compact, stylized demo does not reach the commercial game's visual quality or simulation breadth.

## Starting experience and progression

A new game has **24 residents, $30,000, 13 road cells and six buildings**: two homes, a shop, an industrial lot, power and water facilities. Most buildable land is empty. The first activity is to extend the gateway street and paint a small neighborhood, then watch supplied lots become occupied buildings.

The core decisions are where to extend roads, how much housing to add, which jobs can support it, and when utility or civic expenditure is affordable. Parks, a clinic and balanced employment support attractive neighborhoods; growth expands the tax base and unlocks denser construction.

| Population record | Milestone | New project | One-time grant |
|---|---|---|---:|
| Below 150 | New settlement | Streets, basic zones and services | Starting $30,000 |
| 150 | Growing town | Office district | $6,000 |
| 350 | Rising skyline | Residential towers | $10,000 |
| 600 | Coastal city | Coastal stadium | $15,000 |

Unlocks use the highest population reached, so a temporary loss of residents does not remove an earned project or grant it twice. Reaching 600 is the demo's main progression target; play can continue afterward, but there are no further milestone tiers, map purchases or victory screen.

## Construction and growth

The map is a fixed **48 × 48 grid**, with 10-unit cells and a shoreline that excludes sea from construction. Cardinal streets connect to the regional gateway at `(3,24)`. A dragged road can form an L; construction rejects the complete path if neither supported bend is clear or affordable. Existing road cells are not charged again. Buildings gain frontage from a gateway-connected road within two cells along a cardinal direction.

Zones begin as visible development sites. They need road access, power, water and sufficient demand before construction completes. Residents then move in over time. Growth uses fixed 0.1-day steps; 1× advances approximately 0.12 simulation days per real second, while 3× compresses waiting. Pausing stops development, vehicles and pedestrians, while background audio continues.

| Project | Purchase price | Developed role |
|---|---:|---|
| Street | $80 per new cell | Gateway access, frontage and routes; $0.36/day upkeep per cell |
| Family homes | $45 per zone | 16 residents at level 1; 28 at level 2 |
| Shops and cafés | $60 per zone | 8–12 jobs across two natural growth levels |
| Industry | $55 per zone | 16–40 jobs across four levels; local pollution |
| Residential towers | $1,400 per zone | 64–160 residents across four levels |
| Office district | $2,200 per zone | 48–96 jobs across four levels |
| Coastal stadium | $14,000 once | 3 × 3 reserved site, 64 jobs and neighborhood recreation |

Ordinary homes and shops naturally stop at level 2, preserving their neighborhood silhouettes; separate tower and office tools create the skyline. Higher levels require demand, adequate land value and moderate taxation. Missing utilities stop construction and cause occupied homes to lose residents gradually. Very high taxes or poor happiness also discourage residency. Zoning designates capacity; it does not instantly create a population.

## Services and economy

Utility distribution is deliberately compact: a property needs connected road frontage, available system capacity and a source within a Manhattan-distance service radius. Pipes and power lines are implicit. Allocation protects civic demand before private zones; this makes capacity shortages observable without adding a separate network-construction tool.

| Facility | Purchase | Upkeep/day | Main effect |
|---|---:|---:|---|
| Power station | $6,500 | $32 | 3,000 power units; 26-cell service radius |
| Waterworks | $5,000 | $24 | 2,400 water units; 24-cell service radius |
| Neighborhood park | $700 | $7 | Raises nearby land value and happiness |
| Health clinic | $4,200 | $74 | Adds local health benefits when supplied |
| Coastal stadium | $14,000 | $96 | 64 supplied jobs; recreation within 15 cells; demand of 120 power and 90 water units |

The stadium purchase reserves all nine cells around its center and requires adjacent road access. Its jobs, utility demand and upkeep are counted once. Selecting any part resolves to the center; demolition clears the whole footprint. There is no event schedule, ticket economy or simulated match crowd.

Demand responds to available jobs, population, happiness and tax. The workforce is modeled as 46% of residents. Tax receipts combine residential occupancy and staffed jobs, scaled by the selected rate and happiness. The default rate is 11%; the model accepts 1–25%. Each simulation step books income minus road and facility upkeep. Grants help fund new construction but do not replace an operating economy.

Parks, health, recreation and coastal proximity raise land value; pollution lowers it. The current power facility still applies a generic local pollution penalty even though its art and UI describe renewable generation. Aligning that presentation with an explicit energy tradeoff is a documented next step.

## Visual direction and interface

The art direction is a warm coastal town with recognizable building categories at both map and street distance. Green residential foundations and garden houses, blue retail frames and awnings, and gold industrial yards remain distinguishable before and after development. Teal tower sites and purple office sites lead to balconies, planted terraces, curtain-wall glazing, mullions and roof crowns. The compact stadium has seating tiers, aisles, field markings, floodlights, scoreboards and a glazed concourse.

Reused Kenney models provide compatible buildings, props, vehicles and boats. Five scanned Poly Haven material sets add grass, soil, asphalt, concrete and masonry detail. Original geometry and shaders add smooth multi-crown trees, surface seams, street furniture, windows, animated coastal ripples and shoreline foam. Late-afternoon lighting and a selectable blue-hour treatment support a consistent scene; water motion and night presentation are visual effects.

Road-routed cars and animated roadside walkers make an operating city visibly different from a paused one. Their counts scale with city activity. They are visual agents on real road paths, rather than individual households with daily schedules, lane changes, collision avoidance or traffic-light queues.

The HUD keeps treasury, population, happiness and traffic visible, with demand, milestone progress, map, supply overlays and property inspection nearby. Category-colored icon textures, text labels, lock indicators and a prominent pause banner explain tool state. Construction input is blocked by panels and dialogs. Save/load preserves the city; New City offers an explicit decision about saving existing progress.

## Sound direction

Fifteen original stereo clips provide a gentle 80 BPM score, coast, town, birds, night ambience and ten action cues. The 48-second music loop uses soft struck keys, sustained chords and a sparse melody. Four 24-second environment loops use stylized synthesis rather than field recordings. Successful construction, demolition, growth and milestones have distinct feedback; repeated painting and development cues are throttled.

The mix follows camera distance, location, occupancy, pause and day/night. Music pitch stays constant at 3×. Master, music, ambience and effects sliders, mute and a test cue are available in Sound & Music; preferences persist between ordinary sessions. The generator and source measurements remain editable project assets.

## Deliberate boundaries

The current game has flat buildable terrain, cardinal roads, aggregate residents and jobs, a small service catalogue and one local save slot. It does not implement terrain editing, curved roads, bridges, transit, production chains, education progression, individual citizen lives, disasters, regional expansion or mod support. Assets remain economical meshes, the stadium has compressed scale, and dense-city rendering can hitch during geometry rebuilds. These constraints guide the concrete development priorities in [Implementation](IMPLEMENTATION.md); [Review](REVIEW.md) contains screenshots and the assessment of the delivered experience.
