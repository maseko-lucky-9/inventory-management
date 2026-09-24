-- Idempotent: applied on every startup under an advisory lock (ADR-005).
CREATE TABLE IF NOT EXISTS products (
    id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code        text NOT NULL,
    description text NOT NULL,
    created_at  timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT products_code_key UNIQUE (code)
);

CREATE TABLE IF NOT EXISTS warehouses (
    id         bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code       text NOT NULL,
    name       text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT warehouses_code_key UNIQUE (code)
);

CREATE TABLE IF NOT EXISTS users (
    id            bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    username      text NOT NULL,
    password_hash text NULL,
    CONSTRAINT users_username_key UNIQUE (username)
);

CREATE TABLE IF NOT EXISTS user_warehouses (
    user_id      bigint NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    warehouse_id bigint NOT NULL REFERENCES warehouses (id) ON DELETE RESTRICT,
    PRIMARY KEY (user_id, warehouse_id)
);
CREATE INDEX IF NOT EXISTS user_warehouses_warehouse_id_idx ON user_warehouses (warehouse_id);

-- The CHECK is the backstop behind the guarded decrement (ADR-003).
CREATE TABLE IF NOT EXISTS stock (
    product_id   bigint NOT NULL REFERENCES products (id) ON DELETE RESTRICT,
    warehouse_id bigint NOT NULL REFERENCES warehouses (id) ON DELETE RESTRICT,
    quantity     integer NOT NULL,
    updated_at   timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (product_id, warehouse_id),
    CONSTRAINT stock_quantity_non_negative CHECK (quantity >= 0)
);
CREATE INDEX IF NOT EXISTS stock_warehouse_id_idx ON stock (warehouse_id);

CREATE TABLE IF NOT EXISTS transfer_orders (
    id                       bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    product_id               bigint NOT NULL REFERENCES products (id) ON DELETE RESTRICT,
    source_warehouse_id      bigint NOT NULL REFERENCES warehouses (id) ON DELETE RESTRICT,
    destination_warehouse_id bigint NOT NULL REFERENCES warehouses (id) ON DELETE RESTRICT,
    quantity                 integer NOT NULL,
    created_by               bigint NULL REFERENCES users (id) ON DELETE RESTRICT,
    created_at               timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT transfer_orders_quantity_positive CHECK (quantity > 0),
    CONSTRAINT transfer_orders_distinct_warehouses CHECK (source_warehouse_id <> destination_warehouse_id)
);
CREATE INDEX IF NOT EXISTS transfer_orders_created_at_idx ON transfer_orders (created_at);
