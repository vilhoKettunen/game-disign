here we will combine all the ideas and connect the links to different documents of how this will be created and how it works 

like this [[link to code]]

## The game in one line

You run a company: you take over resource hubs, trade their output, pay
taxes (a fixed 30% + voluntary extra) to the central government, get
political points back, and use those points to take over unclaimed
government nodes. Goal: become the most powerful company in the country.

Constraints: extremely simple, ~10–60 minute playthrough, team of 4
(2 working on it for now).

## STATUS: CORE IS IMPLEMENTED IN CODE

The playable core now exists. Add `GameView` to a scene (it auto-creates
the simulator) and press Play. See [[plan]] for the full map.

| System | Code |
|--------|------|
| Resources & catalog | [[materials]] (materials.cs) |
| Company state | [[Company]] (Company.cs) |
| Nodes / hubs / factories | [[Node]] (Node.cs) |
| Government prices | [[Market]] (Market.cs) |
| Tunables (tick speed, tax, AI) | [[GameConfig]] (GameConfig.cs) |
| Core logic (ticks, taxes, trade, AI, win/lose) | [[GameSimulator]] (GameSimulator.cs) |
| Map + UI | [[GameView]] (GameView.cs) |

## Plan documents (the design docs — questions answered)

- [[core-loop-plan]] — the foundation: player, loop, win/lose, 3 AI
  companies, node map.
- [[trading-system-plan]] — resources, government prices, company trades.
- [[resource-hub-plan]] — hubs, factories, modules (5 slots, 2x prod/store).
- [[tax-system-plan]] — fixed 30% tax, extra %, penalties, bankruptcy.
