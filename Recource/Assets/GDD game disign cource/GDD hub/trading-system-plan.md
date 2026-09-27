# Trading System — Plan (DRAFT)

> Status: QUESTION FRAME — answer the questions, then this becomes the design doc.
> Keep it simple: this is a market, not an economy simulator.
> Back to: [[GDD hub]]

## 1. Purpose

Trading turns resources into money (and money into resources/modules),
so the player can grow the company. It is the engine of the core loop.

## 2. Core questions

- [ ] Q1. How many resources are there?
      (draft: 3 — e.g. Wood, Stone, Oil. Names can change.)

the raw recources that come from nodes/hubs are 

1. wood
2. metal
3. energy
4. water

and then later refined with productions lines into refined recources (thease are made in factoryes that can be upgraded with modules)

1. chips (metal, energy)
2. mecanical parts (metal, water)
3. building materials (wood, energy, water)
4. food (energy, water)

- [ ] Q2. What is the currency?
      (draft: one simple "Cash".)

the recources are the traded currencey and political power is the value difining asset and the goverment dictates the demand and prices for each recource that changes over time based on the war effort (and the tax scales with player production value)

- [ ] Q3. Who do you trade with?
      (draft: one simple market that always exists. No AI traders, no
       contracts, no other players.)

for now no other players but bots theres other simple companies that you can trade with to buy their recource nodes or trade one recource for another by requesting a trade with a compnay and they can accept or not accept (simply for now all companyes will accept as long as youre offering more political power value in the trade then youre requesting from them)

- [ ] Q4. Sell only, or buy too?
      (draft: both. Selling = income. Buying = getting a resource you
       need, e.g. to build modules.)

you can ask a trade in both ways (buying or selling) recources for other recources with companies choosing youre own echange rates (but they will not accept if youre asking for the other company to give you more value)

- [ ] Q5. How are prices set?
      (draft: fixed base price per resource. Optional: small random
       fluctuation (±20%) so it feels alive. Keep optional.)

mostly recources have small fluctuation of ±20% so it feels alive but for refined recources we have rare shortages so theres always one special recource per tax sicle that pays 2x more and generally the range is ±50% for the refined recources political power prices

- [ ] Q6. Per unit or in bundles?
      (draft: per unit with quantity buttons. No warehouses — resources
       sit in one inventory number.)

all the recources combine to each recource numbers and the max is determined by the amount of nodes and modules thats answered in a nother plan

- [ ] Q7. Automatic or manual?
      (draft: manual buttons (sell 1 / sell 10). No auto-sell at first —
       add later only if it feels tedious.)

automaticley pays taxes but trades with other companyes are negotiated separatley with UI by clicking on a company and requesting a trade

- [ ] Q8. What does the market screen look like?
      (draft: a simple list: resource | price | [Sell] [Buy].)

in simple it shows a list of all the recources and their political power prices that the goverment sets and then you can see how much recources different companyes have and you can request a trade with them as long as you give more value they will accept

## 3. Non-goals (keep it simple)

- no supply-and-demand simulation
- no trade routes / logistics
- no player-vs-player market
- no contracts or orders

## 4. Decisions log

| # | Decision | Answer | Date |
|---|----------|--------|------|
| 1 | | | |

## 5. Code

- [[materials]] (Assets/scripts/materials.cs) — where resource logic will live

## 6. Links

- [[GDD hub]] · [[core-loop-plan]]
