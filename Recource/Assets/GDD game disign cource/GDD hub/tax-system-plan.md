# Tax & Politics — Plan (DRAFT)

> Status: QUESTION FRAME — answer the questions, then this becomes the design doc.
> Keep it simple: fixed 30% tax on production + voluntary extra taxes →
> political points → take over government nodes.
> Back to: [[GDD hub]]

## 1. Purpose

The government is the player's "boss". Taxes are the bridge between the
economy (production/trading) and the power goal (government nodes).
This is the path to winning.

## 2. Core questions

- [ ] Q1. How is the fixed 30% tax collected?
      (draft: automatically each tick — 30% of the value of what the
       player produced goes to the government. The player sees a
       "taxes paid" number.)

the defoult tax is taken automatically every 5 ticks (based on the current market value of the player's production over that time and is always shown how much the player needs to pay to warn them) but its based on the current market value of the player's production not the amount of production 

and the player can setup a template on what they want to pay with so choosing the most efficient recources makes senci since then the player can do more between the ticks with their other recources 

and we need to keep showing and updating the tax owed and political power gained and thae amount the player has in political powe and if they wanna add extra to their tax bill thats upcoming it will give a bonus but they will be punished if they dont meet the tax quota by an increase into their tax bill by 20% next time and if they dont meet it they will be charged a higher tax rate and if player fails 2x in a row they go bankrupt

- [ ] Q2. Is the tax paid in money or in resources?
      (draft: money — production is valued in Cash and 30% of that value
       is deducted. Simpler than taking physical resources.)

tax is always payed in recources but its determined by how much the player is producing and how much the market value of that production is based on the goverment set demand for production for the war effort 

so if the war needs more building materials that time the value of thous goes up and therefor you can pay less total materials if you make it into building materials 

- [ ] Q3. How does the voluntary extra tax work?
      (draft: a "Pay extra tax" button — the player picks an amount
       (e.g. 100 / 500 / 1000) and gets political points for it.)

the player can set and extra % rate they want to pay and that will give them a bonus in the amount of political power but they will need to meet the tax quota they set since if they dont they still get punnished 

- [ ] Q4. How many political points per Cash paid?
      (draft: 1 point per 100 Cash. Tune later.)

its not mesured in cash its mesured by the government set demand for production for the war effort so the goverment sets political power amounts that changed depending on the goverments demand for the recources and political power is the value mesurment tool (currencey beeing mesured)

and only recources get traded between companyes 

- [ ] Q5. Do political points expire or decay?
      (draft: no — once earned, always usable. Keep it simple.)

no they do not

- [ ] Q6. What is a government node?
      (draft: a special node on the map that no company owns. Can only
       be taken over by spending political points.)

nodes that are not claimed by any company are government owned nodes

at the start of the game every company starts with 1 node but the other 80-90% are government owned nodes that need to be bought from them with political power

- [ ] Q7. How many government nodes are there?
      (draft: 3–5, each costing more than the last.)

we start with 20 total nodes and 4 companies so 16 government nodes and 4 owned by companies to start

- [ ] Q8. What does a government node grant?
      (options: a passive bonus (+production, +sell price), and/or
       required for the win condition.)

government nodes are recource nodes that just need to be transfered to a company before they start being used to produce their recource type

- [ ] Q9. How does this connect to the win condition?
      (link to [[core-loop-plan]] Q5 — e.g. "own the most government
       nodes" or "own all of them".)

own all of the nodes on the map and concer all companyes to be the last monopoly owning everything on the map

- [ ] Q10. Can the government take anything back?
      (draft: no — no penalties, no confiscation in the simple version.)

goverment can remove a company by making them backrupt if they dont pay enouf taxes 

## 3. The simple version (one screen)

```
[ Your company ]
Taxes this tick:   30% of production   (automatic)
Political points:  12
[ Pay extra tax: 100 / 500 / 1000 ]
Government nodes:  ● ○ ○ ○   (take over with points)
```

## 4. Non-goals (keep it simple)

- no elections, no ranks, no laws
- no government AI that reacts to the player
- no corruption / bribes (yet)

## 5. Decisions log

| # | Decision | Answer | Date |
|---|----------|--------|------|
| 1 | | | |

## 6. Links

- [[GDD hub]] · [[core-loop-plan]] · [[trading-system-plan]]
