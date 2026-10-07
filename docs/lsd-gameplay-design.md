# LSD gameplay design

Status: agreed gameplay direction, pending implementation and prototypes. Recorded October 6, 2026.

## The idea

Build an underground blotter-printing studio in the sewer office. The player collects specialist supplies, prepares batches at a physical bench, prints their own artwork, dries and cuts sheets into tabs, then packages and sells recognizable editions. Equipment upgrades handle repeat production. An anonymous collective supplies the limiting material through existing shop pallets; owned businesses can later receive larger orders in storage the player assigns.

The experience should feel like running a small, creative underground workshop. It should be fun, physical, and different from the native production chains. Materials and processing are fictional game mechanics; realism is not the goal.

The ordinary finished product activates the 0.11 visual and audio treatment the owner is happy with. The existing preview is the artistic foundation, not an implemented production system. See [the preview notes](lsd-preview.md) and [the preserved earlier baseline](lsd-baseline-0.10.1.md).

## Agreed principles

- Production stays in the sewer office for the first release. Relocation to another eligible building can become an optional expansion.
- Use a physical Blotter Bench and a dedicated drying cabinet. Do not repurpose the native quality-improving drying rack as a mandatory drying step.
- Support manual, assisted, and queued production through equipment and our own systems, without depending on native employees.
- Separate formula, material quality, execution yield, and artwork. Production mistakes primarily cost usable output, not quality.
- Create and save artwork once, then reuse it across batches. Drawing is optional creative work, not a compulsory task every production cycle.
- One tab is one sale unit. Packaging follows the familiar capacities of one, five, and twenty tabs.
- Start specialist deliveries at verified shop pallets. Later, register receiving storage at a selectable owned business through its laundering computer.
- Reuse native behavior and S1API where it fits, while keeping delivery, workshop, and artwork rules reusable by other mods.

Names such as **Prism Base**, **Off Register**, and **Sunburn** are working examples. Prices, times, sheet sizes, yields, upgrade costs, and unlock thresholds remain tuning decisions.

## Location and introduction

### Sewer office workshop

The sewer office is the first dedicated workshop: enclosed, underused, and suited to the lab atmosphere. The player places the bench and cabinet within its allowed buildable space rather than receiving a production menu detached from the world.

Its lack of ordinary employee and delivery support shapes the loop. Early supplies must be carried underground. The player handles production or equips machines to repeat it. Finished stock and materials occupy real storage. Business receiving improves logistics without replacing the workshop.

Respect normal access to the sewer office. Introduce the workshop after that access becomes available; the mod should not silently bypass the office's native access conditions. The exact introduction trigger and equipment acquisition route need to be chosen after checking the space in game.

### Discovering the collective

A distinctive sticker or small tag introduces an underground print collective, provisionally called Off Register. Interacting with it starts a brief phone introduction and access to its catalogue. Coordinate this introduction with access to the sewer workshop so players are not directed to inaccessible equipment.

The contact has a name, portrait, personality, and occasional messages. A fully simulated supplier NPC with a walking schedule and handovers is unnecessary for the first release. Character can come from the contact's messages, parcel labels, wrapping, and collection instructions.

## Complete production loop

1. Order specialist material and collect its parcel at a shop pallet or registered business.
2. Buy ordinary printing and packaging supplies from existing shops.
3. Carry supplies to the sewer office and load the Blotter Bench.
4. Select a formula, material grade, and saved artwork edition.
5. Prepare the batch and align and print the sheets.
6. Transfer unfinished sheets to the drying cabinet.
7. Return ready sheets to the bench for cutting.
8. Package matching tabs in retail or bulk quantities.
9. Store, sell, or consume the finished product.

Drying leaves useful time for supply runs, selling, drawing a new edition, or preparing another batch. It must not require standing beside a timer.

## Workshop equipment

### Blotter Bench

A compact bench combines a lightbox, fictional preparation tray, roller press, and paper cutter. Its first version should look improvised from printing equipment and fit comfortably in the sewer office. Placement, camera framing, hand interactions, and usable storage space need a prototype before final models.

The starter bench handles one batch at a time. Upgrades can add a feed tray, cutting attachment, and limited batch queue. Printing and cutting can remain functions of the same bench rather than demanding a new machine for every action.

### Drying cabinet

Use a separate cabinet with sliding trays and visible printed sheets, ideally behind a window. Its job is to turn unfinished printed sheets into sheets ready for cutting. It has a processing timer and finite tray capacity.

Drying does not increase quality. It preserves the batch's formula, quality, edition, and artwork revision. Faster cabinets and additional trays improve throughput. Any optional quality-improvement operation must have separate rules and a clear cost.

The cabinet should visibly show which trays are empty, processing, and ready. Removing sheets, leaving the property, sleeping, or loading a save must not reset or duplicate their processing state.

### Production modes

| Mode | Player interaction | Benefit |
| --- | --- | --- |
| Manual | Prepare, align, print, and cut through the physical interactions | Best achievable yield and direct control |
| Assisted | Set up the batch; fitted attachments handle some repeated actions | Less handling while retaining the workshop feel |
| Queued | Load supplies, choose a saved formula and edition, and start a limited queue | Predictable production while the player does other work |

Automation comes from equipment rather than invisible employees. Machines need real inputs and available output space. They stop safely when supplies run out or outputs fill, and show the reason clearly. Do not make unattended production depend on an employee roster or native transport assignments.

Manual play may recover a modest amount of extra output. Automated yield should still be good enough that upgrades feel worthwhile. Exact throughput and efficiency differences need playtesting.

## Minigames and yield

### Preparation

Use a short calibration puzzle: stabilize a shifting colored pattern with two controls. This is an abstract visual interaction, not a chemical recipe. Accuracy affects how much of the batch remains usable.

### Printing and alignment

Align the sheet with registration guides and guide the roller across it. The satisfying result is seeing the chosen artwork become a physical sheet. Misalignment costs a small amount of usable output around the edges.

### Cutting

Align a cutting guide and pull the cutter to separate finished tabs. Mistakes sacrifice a bounded number of tabs rather than changing the surviving tabs' quality or artwork.

Aim initially for roughly 30–45 seconds of active work per starter batch, excluding drawing and unattended drying. This is a playtest target, not a fixed requirement. Combine or shorten interactions if the full sequence feels repetitive.

Each stage gets a modest loss allowance. Avoid multiplying several small penalties into a disastrous batch. Ordinary mistakes should leave useful output; perfect play improves yield without being required for profitable production.

## Formula, quality, yield, and artwork

| Attribute | Controls | Source |
| --- | --- | --- |
| Formula | Product properties and identity | Selected recipe; one initial fictional recipe |
| Quality | The native quality tier and associated value | Grade of specialist material |
| Yield | Number of usable tabs produced | Batch size, execution, and equipment mode |
| Artwork | Visual identity and edition presentation | Saved design and its immutable revision |

Two batches made from the same grade of material should have the same quality even when their usable tab counts differ. A drawing does not determine strength or quality. Players using presets must have the same production opportunities as players who draw.

### Material grades

Prism Base is the limiting specialist input. Offer a basic grade first and a refined grade through later collective progression. Map grades onto the game's quality system rather than inventing a competing quality scale. Exact grade names and tier mappings remain open.

### Optional improvement step

A later finishing attachment could improve existing stock through a consumable cost and processing time. Define a maximum improvement and prevent repeated free value increases. Start with material grades; add this step only if it creates a useful choice. It is not part of the mandatory drying cabinet operation.

### Mixing

Mixing and additives remain an extension to evaluate. If introduced, they change formula properties while keeping the core LSD presentation recognizable. Do not assume native mixing works merely because a custom product is registered. The initial playable slice uses one formula.

## Blotter artwork and editions

### Editor

Open a flat drawing surface at the bench, using familiar graffiti-style colors, brush sizes, and undo where practical. Add a cutting-grid overlay and a preview of the resulting sheet and individual tab. Simple stamps, symmetry, and duplication would help players make attractive designs without drawing skill.

Support two layouts: one large image spanning the sheet, or a repeated motif on each tab. Offer attractive presets so the artwork system is optional. Full custom drawing is still a first-release goal, not something replaced entirely by presets.

### Reuse and revisions

Create the design before production and select it when starting a batch. Save it in a catalogue, give the edition a name, and reuse it. For example, a red-sun drawing becomes Sunburn; a duplicated design with another palette can become a separate edition.

Editing a saved design creates a new artwork revision. Existing sheets, tabs, and packages keep the revision used when they were produced. Editing tomorrow's batch must not repaint yesterday's inventory.

Formula and artwork remain independently selectable. An edition records their intended association without making the artwork responsible for gameplay properties.

### Physical presentation

Show the correct artwork on sheets, loose tabs, held products, inventory previews, and filled packaging wherever the presentation integration supports it. Keep the edition name and tab quantity readable. Preview the whole sheet, a cut tab, and packaged forms before committing a design.

### Recognition later

Customer comments, edition reputation, requests for particular editions, and a workshop display of successful designs are future additions. Establish ordinary selling first. Artwork-based price bonuses are not an initial balance rule.

## Supplies and shops

| Supply | Source | Function |
| --- | --- | --- |
| Prism Base, in different grades | Collective parcels | Limits production and determines quality |
| Blank blotter stock | Warehouse | Paper for printed sheets |
| Print cartridges | Hardware store | Shared printing consumable |
| Baggies | Existing packaging supply route | Single-tab retail packaging |
| Jars | Existing jar supply route | Five-tab packaging using native jar rules |
| Custom empty bulk sleeves, if added | Warehouse | Supplies for a twenty-tab edition packet |

Blank stock belongs at the warehouse; this supersedes the earlier gas-station suggestion. One cartridge supports the entire color palette. Do not require stocking a different inventory item for every ink color.

The warehouse and hardware listings need exact native shop mappings during implementation. New items should be added without replacing unrelated shop stock.

Custom empty sleeves are a separate feature from custom filled appearances. Their purchasing and consumption rules must be designed explicitly if included; a filled brick visual alone does not create an empty envelope item.

## Tabs, packaging, and sales

One tab is one sale unit. A sheet is a production intermediate containing multiple eventual units; its grid size and maximum output remain tuning decisions. Do not require the sheet size to equal the twenty-tab package capacity.

| Tabs per package | Player-facing form | Native behavior to reuse |
| --- | --- | --- |
| 1 | Baggie with a visible printed tab | Baggie quantity and packaged-product handling |
| 5 | Small tab jar with edition presentation | Jar quantity and packaged-product handling |
| 20 | Flat, sealed edition packet | Brick quantity and packaged-product handling |

Prefer product-specific filled presentation with native packaging identifiers where compatible. Keep other drugs' jars, baggies, and bricks intact. Separate custom packaging definitions may be needed for exclusive empty containers or names; they are not automatically covered by the presentation API.

The twenty-tab packet is folded and sealed at our bench rather than requiring a paper product to use the native physical brick-press interaction. Reuse the quantity and packaged-item data where possible; implement the appropriate paper-handling task separately.

Combine only tabs with the same formula/product identity, quality, edition, and artwork revision. Unpacking preserves those attributes and the exact tab count. Display tab quantities clearly in inventory, storage, sales, and receiving interfaces.

Integrate ordinary customer orders, dealer inventory, handovers, and Product Manager behavior before edition-specific demand. Actual eligibility, unit counting, packing, unpacking, and sales must be verified in game.

## Collective orders and shop pallets

### Initial collection route

Place recognizable specialist parcels at a small selection of verified existing shop delivery pallets. Use the collective's wrapping, symbol, and order label. The phone catalogue shows material grade, quantity, price, delivery time, and collection location.

The contact's personality and physical parcels distinguish this route. There is no need to add a full supplier NPC or duplicate the native supplier progression.

### Shared storage rules

Pallets are shared infrastructure: native purchases may already occupy their storage. Check actual available capacity and reserve space for collective deliveries. Never remove or overwrite native items to make room.

If the location becomes unavailable before arrival, hold the delivery or redirect it to another eligible pallet and send updated instructions. Exact fallback priority needs a prototype. Parcels remain available until collected, without tight pickup deadlines or random loss as a default mechanic.

Persist order identity and state so payment, dispatch, delivery, and collection occur once. Saving, sleeping, reconnecting, or advancing time must not lose or duplicate orders. Capacity must account for all pending reservations.

## Business receiving

### Register a business

Later progression enables Receiving at an owned business's laundering computer. The car wash is one candidate, not the only allowed business. Register an eligible owned business and assign specific racks or cabinets as receiving storage.

Business receiving belongs to the intended progression of the first release, after pallet collection. It does not enable production outside the sewer office.

### Selection flow

1. Open Receiving at the laundering computer.
2. Select or confirm the owned business.
3. Choose Assign storage.
4. Highlight eligible storage within that business and click the desired containers.
5. Confirm and review occupied, reserved, and available receiving capacity.

Use the game's object-selection interface if its clipboard lifecycle integrates cleanly. Otherwise provide a small custom selector with the same straightforward interaction. A list of eligible named containers can also provide an accessible way to review selections.

Registered containers have a receiving badge in their settings and on the receiving screen. The registration belongs to our delivery system. Do not model it as an employee export destination or imply automatic transport to the laundering station. A clipboard shortcut can later edit the same registration.

### Capacity and transport

Count parcel sizes, existing contents, item compatibility, and incoming reservations rather than just counting cabinets. Example: twelve parcel spaces, four occupied, three reserved, five available. That example assumes equal parcel footprints; real capacity follows the actual items.

Accepted shipments retain their reservations until completion or cancellation. Assigned containers cannot be removed while they hold receiving goods or active reservations. Release empty registrations explicitly and explain capacity restrictions before the player buys another order.

Deliveries remain at the business until the player transports them to the sewer office. Greater receiving capacity and a vehicle make larger supply runs worthwhile. Future standing orders can refill within configured limits, but need clear spending and capacity controls before implementation.

## Progression and balance

The intended progression is small pallet orders and manual batches, followed by better material grades and assisted equipment, then business receiving and queued production. Specific unlock milestones and prices are undecided.

Limit throughput through specialist material availability, equipment capacity, drying time, and storage. Larger orders should improve convenience without making unlimited high-profit output automatic. Manual production remains satisfying while equipment removes repetitive handling.

Avoid turning drawing, parcel deadlines, or cartridge shopping into constant chores. There should be useful things to do while batches dry and reasons to expand the workshop without filling it with redundant machines.

## Consumption and the approved effect

Activate the approved 0.11 visual and audio treatment through normal product consumption. Keep quality primarily about value and material progression; premium stock must not automatically increase distortion beyond the look already approved.

Use the current three-minute preview duration as an initial playtest value. Keep the existing comfort and configurable effect controls. An integrated consumption controller must define starting, refreshing/repeating consumption, ending, and cleanup without indefinitely stacking effects.

Handle pause, camera replacement, product changes, save transitions, and disconnects deliberately. Restore owned rendering and audio changes when the effect ends or fails. Keep the preview and emergency reset useful for development while product consumption becomes the normal gameplay trigger.

## Reusable systems and S1API

| System | What another mod can register |
| --- | --- |
| Receiving and deliveries | Contacts, catalogues, parcel contents, collection points, receiving eligibility, and capacity rules |
| Workshop equipment | Inputs, outputs, stages, timing, optional minigames, queues, and equipment upgrades |
| Artwork and editions | Drawing surfaces, presets, saved designs, revisions, and product presentation |
| Consumption effects | Start, update, refresh, end, and cleanup behavior |

Keep LSD-specific recipes, names, assets, and progression separate from those services. Build clear boundaries while implementing LSD, then extract a shared library when another mod has a real use for it. Avoid rebuilding features already supplied by S1API.

The installed S1API assembly exposes custom product kinds, presentation and packaging-content profiles, mixing profiles, phone apps, shop integration, custom effects, and save support. Use these where their supported contracts fit. Product registration does not by itself implement production stations, drawing persistence, or delivery reservations.

Register durable item/product identities before native save restoration and perform world-dependent setup after loading. Custom identifiers must be stable and namespaced. Keep the effect integration separate from product catalogue and workshop logic.

## Persistence and multiplayer requirements

Save the formula and product identity, material quality, edition and artwork revision, intermediate sheet state, machine queues, drying progress, orders, and receiving registrations. Preserve real counts across cutting, packaging, unpacking, and collection.

The host owns production results, payments, reservations, and inventory changes. Clients request actions and receive the authoritative result. Lock an active machine interaction against simultaneous conflicting use while allowing different players to draw, collect supplies, or operate different equipment.

Every participating peer needs compatible content definitions and local assets. Saved player-created artwork needs its own bounded persistence and transfer strategy; S1API's presentation profiles do not automatically transmit drawings or textures. Determine whether to store strokes, a rendered image, or both during the artwork prototype.

Require matching artwork revisions for presentation and stacking. Verify late joins and reconnects, and avoid per-tab texture duplication by sharing rendered artwork for each immutable revision.

## Implementation sequence

### Feasibility prototypes

1. Place a temporary bench and cabinet in the sewer office; check build grids, camera framing, access, and saved placement.
2. Create a custom tab with preset art; verify consumption, storage, sale-unit counting, and native packaging compatibility.
3. Prototype graffiti-style drawing on a tabletop sheet and preserve its artwork through production, packaging, and save/load.
4. Add one parcel to a verified shop pallet without disturbing native purchases; test collection and persistence.
5. Open receiving storage selection from the laundering computer and test property filtering and reservations.

### First playable slice

One collective contact, one collection point, one fictional recipe, one bench, a drying cabinet, presets plus one saved custom design, and finished tabs that can be stored, sold, consumed, and restored after saving. Validate with a second player before expanding it.

### First-release completion

Expand to the selected shop pallets, material grades, saved editions and revisions, 1/5/20 packaging, manual/assisted/queued equipment modes, and business receiving. Balance throughput and logistics in the sewer office. The first playable slice is a milestone, not a substitute for the intended release scope.

### Future additions

Optional quality improvement, mixing and additives, edition recognition and special requests, workshop art displays, controlled standing orders, and relocation to other buildings. These should not block the initial loop.

## Relocation later

Restrict placement to the sewer office for release one, but associate every machine with its actual property and position. Avoid baking one permanent location into saved machine data.

A later relocation feature can require finishing active batches, emptying equipment, packing it up, and placing it in another eligible property. The design catalogue remains available across locations. Preserve access and placement validation when expanding the eligible-property rules.

## Open decisions and acceptance checks

Before polishing assets, resolve the actual sewer buildable space, sheet dimensions, parcel footprints, packaging integration, drawing storage/transfer, and clipboard selection behavior. During playtesting, settle costs, quality mappings, losses, drying time, equipment speeds, and progression thresholds.

A release should demonstrate that:

- Manual mistakes affect bounded yield while preserving material-derived quality.
- Cabinet drying completes production without increasing quality.
- Editing artwork does not change existing stock.
- Packing and unpacking preserve exact quantities, quality, formula, and edition.
- Pallet deliveries coexist with native purchases and survive saving and time skips.
- Registered business storage correctly limits orders and incoming reservations.
- Queued equipment stops cleanly for missing inputs or full outputs.
- Normal sales, consumption, save/load, and multiplayer work with the custom product.
- Consuming a standard tab reproduces the approved effect and restores rendering/audio afterward.

## Evidence and references

Local assembly inspection found `SewerOffice`, property buildable/grid APIs, shop `DeliveryBays`, storage capacity/insertion methods, `LaunderingStation`/`LaunderingInterface`, and an `ObjectSelector` accepting property restrictions, object filters, and multiple selections. Native graffiti includes drawing strokes, colors, undo, and texture output. Those findings establish potential integration points, not tested gameplay or reliable hooks.

- [S1API documentation](https://ifbars.github.io/S1API/index.html): supporting product, item, phone, quest, and save systems.
- [Generic custom products and presentation](https://ifbars.github.io/S1API/docs/generic-custom-products.html): product registration, filled packaging visuals, persistence, and multiplayer boundaries. Custom empty packaging definitions and artwork transfer require additional work.
- [Custom product builder](https://ifbars.github.io/S1API/api/S1API.Products.CustomProductDefinitionBuilder.html): registration alone does not provide station recipes; mixing needs explicit configuration.
- [Current effect implementation and verification](lsd-preview.md): the 0.11 preview being integrated.

All gameplay described here is planned. Existing binaries remain the separate visual preview and the repository's other mods until implementation and in-game checks establish the new behavior.
