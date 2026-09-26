# Resource Hubs — Plan (DRAFT)

> Status: QUESTION FRAME — answer the questions, then this becomes the design doc.
> Keep it simple: a hub is a node that produces one resource and has a
> few configurable module slots.
> Back to: [[GDD hub]]

## 1. Purpose

Hubs are how the player gets resources. The player starts with a few and
takes over more to grow the company. Each hub can be configured with
modules (the modular configurable system).

## 2. Core questions

- [ ] Q1. What is a hub, exactly?
      (draft: a node on the map that produces one resource type per tick.)

yes a hub is a node on the map that has a UI element telling how much it produces per tick and nodes can be upgraded by paying for upgrades with taxes funding better infrastructure and connections for the node creates more efficient transportation routes and production

- [ ] Q2. How many hubs are on the map?
      (draft: 6–10, plus the government nodes from [[tax-system-plan

for now there will be 5-20 nodes and 2-4 companies on the map

- [ ] Q3. How does the player take over a hub?
      (draft: pay a claim cost in Cash. Later hubs cost more.)

pay a claim in political power to the goverment to take over goverment nodes or buy the nodes from other companyes or boy out intire companyes to get all their nodes taken over at once

- [ ] Q4. What does an owned hub do?
      (draft: produces its resource into the player's inventory each tick.)

yes thats correct produces its resource into the player's inventory each tick and with thous recources the player can build and upgrade modules to increase production and storage capacity as well as production to pay specific needs for the goverment 

since the goverments needs in recources and what they are willing to pay for each recources changes over time and production needs to be adjusted accordingly so having a verity of recources and production modules is important for the player to be able to meet the goverments needs

and the INVENTORY panel shows simple metrics so the player sees how their resources move: each resource row shows production/consumption per tick as "prod/con" (e.g. 4/2 = +4 produced in, 2 consumed by a factory, net +2 per tick) plus the current stock/limit, and below the list a TOTAL INVENTORY VALUE in PP. this lets the player plan ahead — see which resources are draining, which are piling up, and whether their total inventory value covers the upcoming tax (see [[tax-system-plan]] Q1)

- [ ] Q5. Do different hubs produce different resources?
      (draft: yes — each hub has one resource type.)

yes different hubs produce different resources some hubs can produce multiple types of resources at once
but most only create one type 

the raw recources that come from nodes/ hubs are 

1. wood
2. metal
3. energy
4. water

and then later refined with productions lines into refined recources

1. chips (metal, energy)
2. mecanical parts (metal, water)
3. building materials (wood, energy, water)
4. food (energy, water)

- [ ] Q6. What is a module?
      (draft: a component that plugs into a hub slot and adds or changes
       a function.)

upgrades to recource nodes and upgrades to factories that produce refined recources

- [ ] Q7. Which modules exist (max 3 to start)?
      (options: Production Booster (+rate), Storage (+capacity),
       Trading Post (+sell price). Pick 2–3.)

currentley i only want production boosters and storage boosters each doing one thing 

so simply the production boosters add a 2x multiplier to the production rate and the storage boosters add a 2x multiplier to the storage capacity of that node 

each node has a maximum capacity that cannot be exceeded but can be increased by adding more modules
and that defines how many recources the player can have acces to but the player has a defoult limit thats always increased by the amount and type of nodes they have

- [ ] Q8. How many module slots per hub?
      (draft: 2.)

5 slots to start with per hub/node  and same limit for factories

- [ ] Q9. Where do modules come from?
      (draft: bought with Cash from the market.)

modules are built by combining refined recources so 10 chips and 10 mecanical parts makes a speedup module that increases production rate by 2x and storage modules are made with 10 building materials and 10 food resources

- [ ] Q10. Can the player remove/swap modules?
      (draft: yes, swap freely. No selling back at first.)

no selling back for now just permenent choises so wen its bought it can not be reverted

- [ ] Q11. Can a hub be lost (e.g. taken back by the government)?
      (draft: no — not in the simple version.)

no not in the simple version

## 3. Modular configuration — the simple version

```
Hub (1 resource type)
├── Slot 1 → [module or empty]
└── Slot 2 → [module or empty]
```

Player clicks a hub → sees its slots → picks a module per slot.
That is the whole configuration UI.

## 4. Non-goals (keep it simple)

- no 3D building placement
- no module crafting
- no hub-to-hub connections / paths
- no combat for taking over

## 5. Decisions log

| # | Decision | Answer | Date |
|---|----------|--------|------|
| 1 | | | |

## 6. Links

- [[GDD hub]] · [[trading-system-plan]] · [[core-loop-plan]]
