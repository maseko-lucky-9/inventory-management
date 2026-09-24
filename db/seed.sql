-- Demo data only; every insert is insert-if-absent. Passwords are set at startup from the environment.
INSERT INTO warehouses (code, name)
VALUES ('WH-A', 'Warehouse A'), ('WH-B', 'Warehouse B'), ('WH-C', 'Warehouse C')
ON CONFLICT (code) DO NOTHING;

INSERT INTO users (username)
VALUES ('alice'), ('bob'), ('carol')
ON CONFLICT (username) DO NOTHING;

-- alice is linked to WH-A, bob to WH-B; carol has no links.
INSERT INTO user_warehouses (user_id, warehouse_id)
SELECT u.id, w.id
FROM users u
JOIN warehouses w ON (u.username, w.code) IN (('alice', 'WH-A'), ('bob', 'WH-B'))
ON CONFLICT DO NOTHING;
