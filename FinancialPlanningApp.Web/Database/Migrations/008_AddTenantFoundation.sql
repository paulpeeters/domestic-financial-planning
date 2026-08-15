CREATE TABLE IF NOT EXISTS tenants (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(128) NOT NULL,
    slug VARCHAR(128) NULL,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    UNIQUE KEY ux_tenants_slug (slug)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS user_tenants (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id BIGINT NOT NULL,
    tenant_id BIGINT NOT NULL,
    role VARCHAR(32) NOT NULL,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT fk_user_tenants_user FOREIGN KEY (user_id) REFERENCES app_users(id),
    CONSTRAINT fk_user_tenants_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id),
    UNIQUE KEY ux_user_tenants_user_tenant (user_id, tenant_id),
    INDEX ix_user_tenants_tenant_role (tenant_id, role, is_active)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Add tenant scope columns (nullable in phase 1 for safe rollout).
ALTER TABLE recurring_payment_templates ADD COLUMN tenant_id BIGINT NULL AFTER user_id;
ALTER TABLE payment_executions ADD COLUMN tenant_id BIGINT NULL AFTER user_id;
ALTER TABLE payment_corrections ADD COLUMN tenant_id BIGINT NULL AFTER user_id;
ALTER TABLE payment_template_mappings ADD COLUMN tenant_id BIGINT NULL AFTER user_id;
ALTER TABLE registered_bank_accounts ADD COLUMN tenant_id BIGINT NULL AFTER user_id;
ALTER TABLE registered_credit_cards ADD COLUMN tenant_id BIGINT NULL AFTER user_id;
ALTER TABLE account_monthly_balances ADD COLUMN tenant_id BIGINT NULL AFTER user_id;

-- Add tenant foreign keys.
ALTER TABLE recurring_payment_templates
    ADD CONSTRAINT fk_templates_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id),
    ADD INDEX ix_templates_tenant_active (tenant_id, is_active);

ALTER TABLE payment_executions
    ADD CONSTRAINT fk_payment_exec_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id),
    ADD INDEX ix_payment_exec_tenant_date (tenant_id, execution_date);

ALTER TABLE payment_corrections
    ADD CONSTRAINT fk_payment_corr_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id),
    ADD INDEX ix_payment_corr_tenant_exec (tenant_id, payment_execution_id);

ALTER TABLE payment_template_mappings
    ADD CONSTRAINT fk_ptm_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id),
    ADD INDEX ix_ptm_tenant_period (tenant_id, period_year, period_month);

ALTER TABLE registered_bank_accounts
    ADD CONSTRAINT fk_rba_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id),
    ADD INDEX ix_rba_tenant_active (tenant_id, is_active);

ALTER TABLE registered_credit_cards
    ADD CONSTRAINT fk_rcc_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id),
    ADD INDEX ix_rcc_tenant_active (tenant_id, is_active);

ALTER TABLE account_monthly_balances
    ADD CONSTRAINT fk_amb_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id),
    ADD INDEX ix_amb_tenant_month (tenant_id, year, month);

-- Backfill strategy:
-- 1) create one default tenant per existing user,
-- 2) create OWNER membership,
-- 3) copy tenant_id into existing business records by user_id.

INSERT INTO tenants(name, slug, is_active, created_utc, updated_utc)
SELECT CONCAT('Personal - ', u.email), CONCAT('user-', u.id), 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
FROM app_users u
WHERE NOT EXISTS (
    SELECT 1 FROM tenants t WHERE t.slug = CONCAT('user-', u.id)
);

INSERT INTO user_tenants(user_id, tenant_id, role, is_active, created_utc, updated_utc)
SELECT u.id, t.id, 'OWNER', 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
FROM app_users u
JOIN tenants t ON t.slug = CONCAT('user-', u.id)
WHERE NOT EXISTS (
    SELECT 1 FROM user_tenants ut WHERE ut.user_id = u.id AND ut.tenant_id = t.id
);

UPDATE recurring_payment_templates x
JOIN user_tenants ut ON ut.user_id = x.user_id AND ut.role = 'OWNER' AND ut.is_active = 1
SET x.tenant_id = ut.tenant_id
WHERE x.tenant_id IS NULL;

UPDATE payment_executions x
JOIN user_tenants ut ON ut.user_id = x.user_id AND ut.role = 'OWNER' AND ut.is_active = 1
SET x.tenant_id = ut.tenant_id
WHERE x.tenant_id IS NULL;

UPDATE payment_corrections x
JOIN user_tenants ut ON ut.user_id = x.user_id AND ut.role = 'OWNER' AND ut.is_active = 1
SET x.tenant_id = ut.tenant_id
WHERE x.tenant_id IS NULL;

UPDATE payment_template_mappings x
JOIN user_tenants ut ON ut.user_id = x.user_id AND ut.role = 'OWNER' AND ut.is_active = 1
SET x.tenant_id = ut.tenant_id
WHERE x.tenant_id IS NULL;

UPDATE registered_bank_accounts x
JOIN user_tenants ut ON ut.user_id = x.user_id AND ut.role = 'OWNER' AND ut.is_active = 1
SET x.tenant_id = ut.tenant_id
WHERE x.tenant_id IS NULL;

UPDATE registered_credit_cards x
JOIN user_tenants ut ON ut.user_id = x.user_id AND ut.role = 'OWNER' AND ut.is_active = 1
SET x.tenant_id = ut.tenant_id
WHERE x.tenant_id IS NULL;

UPDATE account_monthly_balances x
JOIN user_tenants ut ON ut.user_id = x.user_id AND ut.role = 'OWNER' AND ut.is_active = 1
SET x.tenant_id = ut.tenant_id
WHERE x.tenant_id IS NULL;

