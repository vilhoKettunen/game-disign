# Core Loop — Plan (DRAFT)

> Status: QUESTION FRAME — answer the questions, check them off, then this becomes the design doc.
> This is the foundation. The other plans depend on the answers here.
> Back to: [[GDD hub]]

## 1. What the game is (from the team)

You run a company. You take over resource hubs and trade their output to
grow. You pay taxes to the central government — a fixed 30% on all
production, plus voluntary extra taxes — and get political points in
return. You spend political points to take over unclaimed government
nodes. Goal: become the most powerful company in the country.

Constraints: extremely simple (no deep systems), ~10–60 minute playthrough.

## 2. Core questions

- [ ] Q1. Who is the player?
      (draft: "You are the CEO of a resource company.")

yes you are a CEO of a resource company and choose what it does with your resources

- [ ] Q2. Confirm or rewrite the core loop in one sentence:
      "Take over a hub → produce a resource → pay taxes → buy
       more hubs/ craft modules → pay extra taxes → get political points →
       take over a government node  take over other companies  or nodes from other companies → win."



- [ ] Q3. Real-time or tick-based?
      (draft: simple ticks — a button or a slow timer advances
       production, taxes and the market once per tick. Easier to balance
       and simpler than continuous time.)

a tick takes one minute so every 5 minutes the tax tick is triggered 

(allow me to edit tic speed in the editor for testing)

- [ ] Q4. What does the world look like?
      (draft: a simple node map — hubs and government nodes laid out on
       a board. No 3D world needed.)

its an extreamly simple flat map of a contrey with many nodes and we need to somehow show a colored plain area of what a company owns all different colors and expands acording to what nodes a company owns.

- [ ] Q5. WIN CONDITION — what exactly makes you "the most powerful company"?
      (pick ONE: a) own the most government nodes, b) reach a score/rank,
       c) own X% of all hubs)

wen a company owns all recource nodes

- [ ] Q6. Is there a lose condition?
      (draft: no hard lose — the game ends when you win. Running out of
       money just means you can't grow.)

yes if you dont pay adequite taxes 2 times in a row

- [ ] Q7. Is there an opponent (AI companies) or only the government?
      (draft: no direct enemies. "Power" is measured by what you own.)

yes 3 other ai companies so they can't just sit idle and wait for you to take over.
they do simple actions and try to expand the same as you do.

- [ ] Q8. First 5 minutes — what does the player do?
      (draft: start with 1 hub + political power → produce → trade → take over a
       second hub → see the tax screen → the loop is clear.)

so the player chooses what they want to expand to what they want to pay in and what they want to build.

the player gets some starting political power enouf to buy first 2 nodes and the more nodes a company owns the more they need to pay for the next node.

- [ ] Q9. Scope line — what is definitely NOT in the game?
      (e.g. no combat, no employee management, no logistics routes, no
       player-vs-player trading. List the cuts here.)

No combat No enplyees No logistics routes No player-vs-player trading (there is player to NPC company trading since its singleplayer for now)

## 3. Decisions log (fill in as you answer)

| # | Decision | Answer | Date |
|---|----------|--------|------|
| 1 | | | |

## 4. Links

- [[GDD hub]]
- [[trading-system-plan]] · [[resource-hub-plan]] · [[tax-system-plan]]
