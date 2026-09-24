-- Optional demo data for local play, added after the four-hour build. Applied after seed.sql, in the same
-- transaction, only when Seed:DemoData is true (off by default: tests, CI and production never load it).
-- Every insert is insert-if-absent, stock included, so a restart never resets a level changed by playing.
-- Ids are resolved by code; no transfer orders are seeded.

-- Three factories and five distribution centres, beside the seeded WH-A, WH-B and WH-C.
INSERT INTO warehouses (code, name)
VALUES
    ('FAC-JHB', 'Johannesburg factory'),
    ('FAC-DBN', 'Durban factory'),
    ('FAC-PTA', 'Pretoria factory'),
    ('DC-CPT', 'Cape Town distribution centre'),
    ('DC-PLZ', 'Gqeberha distribution centre'),
    ('DC-BFN', 'Bloemfontein distribution centre'),
    ('DC-ELS', 'East London distribution centre'),
    ('DC-PLK', 'Polokwane distribution centre')
ON CONFLICT (code) DO NOTHING;

-- Eight product families, five products each; the code prefix names the family.
INSERT INTO products (code, description)
VALUES
    ('BRG-6204-2RS', 'Deep groove ball bearing 6204-2RS, 20 x 47 x 14 mm, rubber sealed'),
    ('BRG-6305-ZZ', 'Deep groove ball bearing 6305-ZZ, 25 x 62 x 17 mm, metal shielded'),
    ('BRG-22210-E', 'Spherical roller bearing 22210 E, 50 x 90 x 23 mm'),
    ('BRG-30206', 'Tapered roller bearing 30206, 30 x 62 x 17.25 mm'),
    ('BRG-UCP205', 'Pillow block bearing unit UCP205, 25 mm bore, cast iron housing'),
    ('BLT-M8X30-88', 'Hex bolt M8 x 30 mm, grade 8.8, zinc plated, box of 100'),
    ('BLT-M10X50-88', 'Hex bolt M10 x 50 mm, grade 8.8, zinc plated, box of 100'),
    ('BLT-M12X60-109', 'Hex bolt M12 x 60 mm, grade 10.9, self colour, box of 50'),
    ('BLT-M16X80-HDG', 'Hex bolt M16 x 80 mm, grade 8.8, hot-dip galvanised, box of 25'),
    ('BLT-M20X100-SS', 'Hex bolt M20 x 100 mm, stainless steel A4-70, box of 10'),
    ('MTR-IE3-0.75KW-4P', 'Three-phase induction motor 0.75 kW, 4-pole, IE3, foot mounted'),
    ('MTR-IE3-4KW-4P', 'Three-phase induction motor 4 kW, 4-pole, IE3, foot mounted'),
    ('MTR-IE3-11KW-2P', 'Three-phase induction motor 11 kW, 2-pole, IE3, foot mounted'),
    ('MTR-IE3-22KW-4P', 'Three-phase induction motor 22 kW, 4-pole, IE3, flange mounted'),
    ('MTR-GBX-1.5KW', 'Helical geared motor 1.5 kW, 20:1 ratio, 70 rpm output'),
    ('VLV-BALL-DN25', 'Ball valve DN25, stainless steel 316, full bore, threaded, PN40'),
    ('VLV-BALL-DN50', 'Ball valve DN50, carbon steel, full bore, flanged, PN16'),
    ('VLV-GATE-DN80', 'Gate valve DN80, cast iron, rising stem, flanged, PN16'),
    ('VLV-CHK-DN50', 'Swing check valve DN50, bronze, threaded, PN16'),
    ('VLV-BFLY-DN100', 'Butterfly valve DN100, wafer pattern, EPDM seat, lever operated'),
    ('PMP-CEN-2.2KW', 'End-suction centrifugal pump 2.2 kW, 50 mm outlet, three-phase'),
    ('PMP-CEN-5.5KW', 'End-suction centrifugal pump 5.5 kW, 65 mm outlet, three-phase'),
    ('PMP-SUB-1.1KW', 'Submersible drainage pump 1.1 kW, 230 V, float switch'),
    ('PMP-DIA-25', 'Air-operated double diaphragm pump, 25 mm ports, aluminium body'),
    ('PMP-GEAR-10', 'External gear pump 10 l/min, cast iron, for lubricating oil'),
    ('GSK-SW-DN50', 'Spiral wound gasket DN50 PN40, 316L winding, graphite filler'),
    ('GSK-SW-DN100', 'Spiral wound gasket DN100 PN40, 316L winding, graphite filler'),
    ('GSK-CNAF-DN80', 'Compressed non-asbestos fibre gasket DN80 PN16, 3 mm thick'),
    ('GSK-PTFE-DN25', 'PTFE envelope gasket DN25 PN16, for corrosive service'),
    ('GSK-RTJ-R24', 'Ring type joint gasket R24, soft iron, oval section'),
    ('CBL-PVC-2.5-3C', 'PVC insulated power cable 2.5 mm2, 3-core, 100 m drum'),
    ('CBL-SWA-16-4C', 'Steel wire armoured cable 16 mm2, 4-core, 100 m drum'),
    ('CBL-FLEX-1.5-3C', 'Rubber sheathed flexible cord 1.5 mm2, 3-core, 100 m drum'),
    ('CBL-INST-0.5-10P', 'Instrumentation cable 0.5 mm2, 10 pair, overall screen, 100 m drum'),
    ('CBL-CAT6-305', 'Cat 6 U/UTP data cable, solid copper, 305 m box'),
    ('FLT-AIR-290', 'Panel air filter element 290 x 290 x 48 mm, G4 class'),
    ('FLT-OIL-M20', 'Spin-on oil filter, M20 x 1.5 thread, 10 micron'),
    ('FLT-HYD-10MU', 'Hydraulic return line filter element, 10 micron, glass fibre'),
    ('FLT-WTR-20IN', 'Water filter cartridge 20 inch, 5 micron, pleated polyester'),
    ('FLT-FUEL-30MU', 'Fuel water separator element, 30 micron')
ON CONFLICT (code) DO NOTHING;

-- alice: WH-A plus the Johannesburg and Pretoria factories, Cape Town and Gqeberha.
-- bob: WH-B plus the Durban factory, Gqeberha (shared with alice), Bloemfontein, East London and Polokwane.
-- carol stays with no links on purpose: the spec needs a user who sees nothing. WH-C stays unlinked.
INSERT INTO user_warehouses (user_id, warehouse_id)
SELECT u.id, w.id
FROM (VALUES
    ('alice', 'FAC-JHB'), ('alice', 'FAC-PTA'), ('alice', 'DC-CPT'), ('alice', 'DC-PLZ'),
    ('bob', 'FAC-DBN'), ('bob', 'DC-PLZ'), ('bob', 'DC-BFN'), ('bob', 'DC-ELS'), ('bob', 'DC-PLK')
) AS link (username, warehouse_code)
JOIN users u ON u.username = link.username
JOIN warehouses w ON w.code = link.warehouse_code
ON CONFLICT (user_id, warehouse_id) DO NOTHING;

-- One row per product: its level at each site, NULL where the site holds no row. Each family is made in one
-- factory (bearings, bolts and motors in Johannesburg; valves, pumps and gaskets in Durban; cables and filters
-- in Pretoria) and held in a few distribution centres. Zeros and single digits are there to demo a refused transfer.
INSERT INTO stock (product_id, warehouse_id, quantity)
SELECT p.id, w.id, site.quantity
FROM (VALUES
    -- product              FAC-JHB FAC-DBN FAC-PTA DC-CPT DC-PLZ DC-BFN DC-ELS DC-PLK WH-A  WH-B
    ('BRG-6204-2RS',         2400,   NULL,   NULL,   180,   95,    0,     NULL,  NULL,  40,   NULL),
    ('BRG-6305-ZZ',          1850,   NULL,   NULL,   120,   4,     60,    NULL,  NULL,  NULL, NULL),
    ('BRG-22210-E',          420,    NULL,   NULL,   18,    12,    NULL,  NULL,  NULL,  6,    NULL),
    ('BRG-30206',            960,    NULL,   NULL,   75,    NULL,  30,    NULL,  NULL,  12,   NULL),
    ('BRG-UCP205',           640,    NULL,   NULL,   0,     44,    25,    NULL,  NULL,  NULL, NULL),
    ('BLT-M8X30-88',         3000,   NULL,   NULL,   850,   NULL,  600,   420,   300,   NULL, NULL),
    ('BLT-M10X50-88',        2800,   NULL,   NULL,   700,   NULL,  450,   0,     260,   NULL, NULL),
    ('BLT-M12X60-109',       1500,   NULL,   NULL,   320,   NULL,  NULL,  180,   8,     NULL, NULL),
    ('BLT-M16X80-HDG',       1200,   NULL,   NULL,   240,   NULL,  150,   90,    NULL,  NULL, NULL),
    ('BLT-M20X100-SS',       600,    NULL,   NULL,   5,     NULL,  40,    NULL,  20,    NULL, NULL),
    ('MTR-IE3-0.75KW-4P',    480,    NULL,   NULL,   NULL,  36,    24,    NULL,  NULL,  NULL, 10),
    ('MTR-IE3-4KW-4P',       350,    NULL,   NULL,   NULL,  20,    3,     NULL,  NULL,  NULL, NULL),
    ('MTR-IE3-11KW-2P',      220,    NULL,   NULL,   NULL,  8,     12,    NULL,  NULL,  NULL, 2),
    ('MTR-IE3-22KW-4P',      140,    NULL,   NULL,   NULL,  0,     6,     NULL,  NULL,  NULL, NULL),
    ('MTR-GBX-1.5KW',        300,    NULL,   NULL,   NULL,  15,    NULL,  NULL,  NULL,  NULL, 4),
    ('VLV-BALL-DN25',        NULL,   1100,   NULL,   90,    60,    35,    NULL,  NULL,  NULL, NULL),
    ('VLV-BALL-DN50',        NULL,   640,    NULL,   48,    6,     22,    NULL,  NULL,  NULL, NULL),
    ('VLV-GATE-DN80',        NULL,   380,    NULL,   14,    NULL,  9,     NULL,  NULL,  NULL, NULL),
    ('VLV-CHK-DN50',         NULL,   520,    NULL,   0,     26,    18,    NULL,  NULL,  NULL, NULL),
    ('VLV-BFLY-DN100',       NULL,   300,    NULL,   20,    11,    NULL,  NULL,  NULL,  NULL, NULL),
    ('PMP-CEN-2.2KW',        NULL,   260,    NULL,   18,    NULL,  NULL,  9,     12,    NULL, NULL),
    ('PMP-CEN-5.5KW',        NULL,   180,    NULL,   7,     NULL,  NULL,  4,     NULL,  NULL, NULL),
    ('PMP-SUB-1.1KW',        NULL,   420,    NULL,   35,    NULL,  NULL,  22,    30,    NULL, NULL),
    ('PMP-DIA-25',           NULL,   150,    NULL,   2,     NULL,  NULL,  NULL,  6,     NULL, NULL),
    ('PMP-GEAR-10',          NULL,   200,    NULL,   10,    NULL,  NULL,  0,     8,     NULL, NULL),
    ('GSK-SW-DN50',          NULL,   2200,   NULL,   NULL,  300,   120,   150,   NULL,  NULL, 60),
    ('GSK-SW-DN100',         NULL,   1600,   NULL,   NULL,  180,   0,     90,    NULL,  NULL, NULL),
    ('GSK-CNAF-DN80',        NULL,   2500,   NULL,   NULL,  400,   160,   220,   NULL,  NULL, 45),
    ('GSK-PTFE-DN25',        NULL,   1800,   NULL,   NULL,  5,     NULL,  110,   NULL,  NULL, 30),
    ('GSK-RTJ-R24',          NULL,   700,    NULL,   NULL,  40,    25,    NULL,  NULL,  NULL, NULL),
    ('CBL-PVC-2.5-3C',       NULL,   NULL,   900,    120,   NULL,  80,    NULL,  60,    25,   NULL),
    ('CBL-SWA-16-4C',        NULL,   NULL,   400,    30,    NULL,  12,    NULL,  0,     NULL, NULL),
    ('CBL-FLEX-1.5-3C',      NULL,   NULL,   750,    90,    NULL,  NULL,  NULL,  45,    15,   NULL),
    ('CBL-INST-0.5-10P',     NULL,   NULL,   320,    16,    NULL,  8,     NULL,  3,     NULL, NULL),
    ('CBL-CAT6-305',         NULL,   NULL,   1000,   150,   NULL,  70,    NULL,  40,    20,   NULL),
    ('FLT-AIR-290',          NULL,   NULL,   1400,   NULL,  160,   NULL,  90,    70,    NULL, NULL),
    ('FLT-OIL-M20',          NULL,   NULL,   2600,   NULL,  300,   NULL,  0,     150,   NULL, NULL),
    ('FLT-HYD-10MU',         NULL,   NULL,   800,    NULL,  45,    NULL,  30,    NULL,  NULL, NULL),
    ('FLT-WTR-20IN',         NULL,   NULL,   1900,   NULL,  220,   NULL,  140,   7,     NULL, NULL),
    ('FLT-FUEL-30MU',        NULL,   NULL,   1100,   NULL,  NULL,  NULL,  60,    35,    NULL, NULL)
) AS grid (product_code, fac_jhb, fac_dbn, fac_pta, dc_cpt, dc_plz, dc_bfn, dc_els, dc_plk, wh_a, wh_b)
CROSS JOIN LATERAL (VALUES
    ('FAC-JHB', grid.fac_jhb), ('FAC-DBN', grid.fac_dbn), ('FAC-PTA', grid.fac_pta),
    ('DC-CPT', grid.dc_cpt), ('DC-PLZ', grid.dc_plz), ('DC-BFN', grid.dc_bfn), ('DC-ELS', grid.dc_els),
    ('DC-PLK', grid.dc_plk), ('WH-A', grid.wh_a), ('WH-B', grid.wh_b)
) AS site (warehouse_code, quantity)
JOIN products p ON p.code = grid.product_code
JOIN warehouses w ON w.code = site.warehouse_code
WHERE site.quantity IS NOT NULL
ON CONFLICT (product_id, warehouse_id) DO NOTHING;
