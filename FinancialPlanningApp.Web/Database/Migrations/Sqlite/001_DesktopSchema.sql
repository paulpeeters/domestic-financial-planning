PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS tenants (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    short_name TEXT NULL,
    slug TEXT NULL UNIQUE,
    is_active INTEGER NOT NULL DEFAULT 1,
    created_utc TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_utc TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE TABLE IF NOT EXISTS app_users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    email TEXT NOT NULL UNIQUE,
    first_name TEXT NULL,
    last_name TEXT NULL,
    avatar_url TEXT NULL,
    password_hash TEXT NOT NULL,
    is_global_admin INTEGER NOT NULL DEFAULT 0,
    is_active INTEGER NOT NULL DEFAULT 1,
    preferred_tenant_id INTEGER NULL,
    created_utc TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now')),
    FOREIGN KEY (preferred_tenant_id) REFERENCES tenants(id)
);

CREATE INDEX IF NOT EXISTS ix_app_users_active ON app_users(is_active);
CREATE INDEX IF NOT EXISTS ix_app_users_preferred_tenant ON app_users(preferred_tenant_id);

CREATE TABLE IF NOT EXISTS user_tenants (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    tenant_id INTEGER NOT NULL,
    role TEXT NOT NULL,
    is_active INTEGER NOT NULL DEFAULT 1,
    display_first_name TEXT NULL,
    display_last_name TEXT NULL,
    display_avatar_url TEXT NULL,
    created_utc TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_utc TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now')),
    FOREIGN KEY (user_id) REFERENCES app_users(id),
    FOREIGN KEY (tenant_id) REFERENCES tenants(id),
    UNIQUE (user_id, tenant_id)
);

CREATE INDEX IF NOT EXISTS ix_user_tenants_tenant_role ON user_tenants(tenant_id, role, is_active);

CREATE TABLE IF NOT EXISTS recurring_payment_templates (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    tenant_id INTEGER NOT NULL,
    description TEXT NOT NULL,
    matching_keywords TEXT NULL,
    display_order INTEGER NOT NULL,
    periodicity TEXT NOT NULL,
    payment_month INTEGER NULL,
    payment_months TEXT NULL,
    payment_day INTEGER NULL,
    payment_lag_months INTEGER NOT NULL DEFAULT 0,
    payment_method TEXT NOT NULL,
    amount NUMERIC NOT NULL,
    amount_mode TEXT NOT NULL DEFAULT 'Fixed',
    monthly_amounts_json TEXT NULL,
    normalized_monthly_amount NUMERIC NOT NULL,
    active_from TEXT NOT NULL,
    active_until TEXT NULL,
    is_active INTEGER NOT NULL,
    created_utc TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now')),
    FOREIGN KEY (user_id) REFERENCES app_users(id),
    FOREIGN KEY (tenant_id) REFERENCES tenants(id)
);

CREATE INDEX IF NOT EXISTS ix_templates_user_active ON recurring_payment_templates(user_id, is_active);
CREATE INDEX IF NOT EXISTS ix_templates_tenant_active ON recurring_payment_templates(tenant_id, is_active);

CREATE TABLE IF NOT EXISTS payment_executions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    tenant_id INTEGER NOT NULL,
    template_id INTEGER NULL,
    execution_date TEXT NOT NULL,
    description TEXT NOT NULL,
    payment_method TEXT NOT NULL,
    amount NUMERIC NOT NULL,
    source_type TEXT NOT NULL,
    source_reference TEXT NULL,
    source_sequence TEXT NULL,
    source_account_number TEXT NULL,
    source_card_number TEXT NULL,
    execution_type TEXT NULL,
    mapping_status TEXT NOT NULL DEFAULT 'UNMAPPED',
    mapped_template_id INTEGER NULL,
    mapped_period_year INTEGER NULL,
    mapped_period_month INTEGER NULL,
    notes TEXT NULL,
    created_utc TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now')),
    FOREIGN KEY (user_id) REFERENCES app_users(id),
    FOREIGN KEY (tenant_id) REFERENCES tenants(id),
    FOREIGN KEY (template_id) REFERENCES recurring_payment_templates(id),
    FOREIGN KEY (mapped_template_id) REFERENCES recurring_payment_templates(id)
);

CREATE INDEX IF NOT EXISTS ix_payment_executions_user_date ON payment_executions(user_id, execution_date);
CREATE INDEX IF NOT EXISTS ix_payment_exec_tenant_date ON payment_executions(tenant_id, execution_date);
CREATE INDEX IF NOT EXISTS ix_payment_exec_mapping_tenant ON payment_executions(user_id, tenant_id, mapping_status, mapped_period_year, mapped_period_month);
CREATE INDEX IF NOT EXISTS ix_payment_exec_source_ref_tenant ON payment_executions(user_id, tenant_id, source_reference);
CREATE INDEX IF NOT EXISTS ix_payment_exec_source_seq_tenant ON payment_executions(user_id, tenant_id, source_type, source_sequence, source_account_number, source_card_number);
CREATE INDEX IF NOT EXISTS ix_payment_executions_dedupe_tenant ON payment_executions(user_id, tenant_id, source_reference, execution_date, amount, description);

CREATE TABLE IF NOT EXISTS payment_corrections (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    tenant_id INTEGER NOT NULL,
    payment_execution_id INTEGER NOT NULL,
    correction_type TEXT NOT NULL,
    amount_delta NUMERIC NOT NULL,
    reason TEXT NOT NULL,
    created_utc TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now')),
    FOREIGN KEY (user_id) REFERENCES app_users(id),
    FOREIGN KEY (tenant_id) REFERENCES tenants(id),
    FOREIGN KEY (payment_execution_id) REFERENCES payment_executions(id)
);

CREATE INDEX IF NOT EXISTS ix_payment_corrections_execution ON payment_corrections(payment_execution_id);

CREATE TABLE IF NOT EXISTS payment_template_mappings (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    tenant_id INTEGER NOT NULL,
    execution_id INTEGER NOT NULL UNIQUE,
    template_id INTEGER NOT NULL,
    period_year INTEGER NOT NULL,
    period_month INTEGER NOT NULL,
    mapped_by TEXT NOT NULL,
    mapped_utc TEXT NOT NULL,
    confidence_note TEXT NULL,
    FOREIGN KEY (user_id) REFERENCES app_users(id),
    FOREIGN KEY (tenant_id) REFERENCES tenants(id),
    FOREIGN KEY (execution_id) REFERENCES payment_executions(id),
    FOREIGN KEY (template_id) REFERENCES recurring_payment_templates(id)
);

CREATE INDEX IF NOT EXISTS ix_ptm_tenant_period ON payment_template_mappings(tenant_id, period_year, period_month);

CREATE TABLE IF NOT EXISTS registered_bank_accounts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    tenant_id INTEGER NOT NULL,
    display_name TEXT NOT NULL,
    account_number TEXT NOT NULL,
    is_active INTEGER NOT NULL DEFAULT 1,
    created_utc TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now')),
    FOREIGN KEY (user_id) REFERENCES app_users(id),
    FOREIGN KEY (tenant_id) REFERENCES tenants(id),
    UNIQUE (tenant_id, account_number)
);

CREATE INDEX IF NOT EXISTS ix_registered_bank_accounts_user_id ON registered_bank_accounts(user_id);
CREATE INDEX IF NOT EXISTS ix_rba_tenant_active ON registered_bank_accounts(tenant_id, is_active);

CREATE TABLE IF NOT EXISTS registered_credit_cards (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    tenant_id INTEGER NOT NULL,
    display_name TEXT NOT NULL,
    card_number TEXT NOT NULL,
    is_active INTEGER NOT NULL DEFAULT 1,
    created_utc TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now')),
    FOREIGN KEY (user_id) REFERENCES app_users(id),
    FOREIGN KEY (tenant_id) REFERENCES tenants(id),
    UNIQUE (tenant_id, card_number)
);

CREATE INDEX IF NOT EXISTS ix_registered_credit_cards_user_id ON registered_credit_cards(user_id);
CREATE INDEX IF NOT EXISTS ix_rcc_tenant_active ON registered_credit_cards(tenant_id, is_active);

CREATE TABLE IF NOT EXISTS account_monthly_balances (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    tenant_id INTEGER NOT NULL,
    account_number TEXT NOT NULL,
    year INTEGER NOT NULL,
    month INTEGER NOT NULL,
    opening_balance NUMERIC NULL,
    closing_balance NUMERIC NOT NULL,
    source_reference TEXT NULL,
    created_utc TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_utc TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now')),
    FOREIGN KEY (user_id) REFERENCES app_users(id),
    FOREIGN KEY (tenant_id) REFERENCES tenants(id),
    UNIQUE (tenant_id, account_number, year, month)
);

CREATE INDEX IF NOT EXISTS ix_account_monthly_balances_user_id ON account_monthly_balances(user_id);
CREATE INDEX IF NOT EXISTS ix_amb_tenant_month ON account_monthly_balances(tenant_id, year, month);

CREATE TABLE IF NOT EXISTS app_settings (
    key TEXT NOT NULL PRIMARY KEY,
    value TEXT NULL,
    updated_utc TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

INSERT INTO app_settings(key, value)
SELECT 'allow_self_registration', 'true'
WHERE NOT EXISTS (SELECT 1 FROM app_settings WHERE key = 'allow_self_registration');

CREATE TABLE IF NOT EXISTS auth_login_attempts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    attempted_utc TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now')),
    email TEXT NULL,
    user_id INTEGER NULL,
    is_success INTEGER NOT NULL,
    failure_reason TEXT NULL,
    ip_address TEXT NULL,
    user_agent TEXT NULL,
    FOREIGN KEY (user_id) REFERENCES app_users(id)
);

CREATE INDEX IF NOT EXISTS ix_auth_login_attempts_attempted ON auth_login_attempts(attempted_utc);
CREATE INDEX IF NOT EXISTS ix_auth_login_attempts_email ON auth_login_attempts(email);
CREATE INDEX IF NOT EXISTS ix_auth_login_attempts_success ON auth_login_attempts(is_success);

CREATE TABLE IF NOT EXISTS mail_settings (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    tenant_id INTEGER NULL,
    scope_key TEXT NOT NULL UNIQUE,
    is_enabled INTEGER NOT NULL DEFAULT 0,
    provider TEXT NOT NULL DEFAULT 'Disabled',
    from_name TEXT NULL,
    from_email TEXT NULL,
    base_url TEXT NULL,
    api_key TEXT NULL,
    smtp_host TEXT NULL,
    smtp_port INTEGER NULL,
    smtp_username TEXT NULL,
    smtp_password TEXT NULL,
    smtp_use_ssl INTEGER NOT NULL DEFAULT 1,
    updated_utc TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now')),
    FOREIGN KEY (tenant_id) REFERENCES tenants(id)
);

CREATE INDEX IF NOT EXISTS ix_mail_settings_tenant ON mail_settings(tenant_id);

INSERT INTO mail_settings(scope_key, tenant_id, is_enabled, provider, smtp_use_ssl)
SELECT 'global', NULL, 0, 'Disabled', 1
WHERE NOT EXISTS (SELECT 1 FROM mail_settings WHERE scope_key = 'global');

CREATE TABLE IF NOT EXISTS password_reset_tokens (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    token_hash TEXT NOT NULL UNIQUE,
    created_utc TEXT NOT NULL DEFAULT (STRFTIME('%Y-%m-%dT%H:%M:%fZ', 'now')),
    expires_utc TEXT NOT NULL,
    used_utc TEXT NULL,
    request_ip TEXT NULL,
    user_agent TEXT NULL,
    FOREIGN KEY (user_id) REFERENCES app_users(id)
);

CREATE INDEX IF NOT EXISTS ix_password_reset_tokens_user_active ON password_reset_tokens(user_id, used_utc, expires_utc);
