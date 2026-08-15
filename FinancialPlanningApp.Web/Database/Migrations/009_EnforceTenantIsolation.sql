-- Enforce tenant isolation on all business tables.
-- Assumes migration 008 has backfilled tenant_id values.

ALTER TABLE recurring_payment_templates MODIFY COLUMN tenant_id BIGINT NOT NULL;
ALTER TABLE payment_executions MODIFY COLUMN tenant_id BIGINT NOT NULL;
ALTER TABLE payment_corrections MODIFY COLUMN tenant_id BIGINT NOT NULL;
ALTER TABLE payment_template_mappings MODIFY COLUMN tenant_id BIGINT NOT NULL;
ALTER TABLE registered_bank_accounts MODIFY COLUMN tenant_id BIGINT NOT NULL;
ALTER TABLE registered_credit_cards MODIFY COLUMN tenant_id BIGINT NOT NULL;
ALTER TABLE account_monthly_balances MODIFY COLUMN tenant_id BIGINT NOT NULL;

-- Registry uniqueness should be tenant-wide (not user-wide).
-- Keep dedicated user_id indexes so existing FKs on user_id remain valid
-- when replacing legacy unique indexes.
ALTER TABLE registered_bank_accounts
    ADD INDEX ix_registered_bank_accounts_user_id (user_id);

ALTER TABLE registered_bank_accounts DROP INDEX ux_registered_bank_accounts_user_account;
ALTER TABLE registered_bank_accounts
    ADD UNIQUE KEY ux_registered_bank_accounts_tenant_account (tenant_id, account_number);

ALTER TABLE registered_credit_cards
    ADD INDEX ix_registered_credit_cards_user_id (user_id);

ALTER TABLE registered_credit_cards DROP INDEX ux_registered_credit_cards_user_card;
ALTER TABLE registered_credit_cards
    ADD UNIQUE KEY ux_registered_credit_cards_tenant_card (tenant_id, card_number);

ALTER TABLE account_monthly_balances
    ADD INDEX ix_account_monthly_balances_user_id (user_id);

ALTER TABLE account_monthly_balances DROP INDEX ux_account_monthly_balance_user_account_month;
ALTER TABLE account_monthly_balances
    ADD UNIQUE KEY ux_account_monthly_balance_tenant_account_month (tenant_id, account_number, year, month);

-- Tenant-aware lookup/dedupe indexes.
ALTER TABLE payment_executions DROP INDEX ix_payment_exec_source_ref;
ALTER TABLE payment_executions
    ADD INDEX ix_payment_exec_source_ref_tenant (user_id, tenant_id, source_reference(191));

ALTER TABLE payment_executions DROP INDEX ix_payment_exec_source_seq;
ALTER TABLE payment_executions
    ADD INDEX ix_payment_exec_source_seq_tenant (user_id, tenant_id, source_type, source_sequence, source_account_number, source_card_number);

ALTER TABLE payment_executions DROP INDEX ix_payment_executions_dedupe;
ALTER TABLE payment_executions
    ADD INDEX ix_payment_executions_dedupe_tenant (user_id, tenant_id, source_reference(128), execution_date, amount, description(128));

ALTER TABLE payment_executions DROP INDEX ix_payment_exec_mapping;
ALTER TABLE payment_executions
    ADD INDEX ix_payment_exec_mapping_tenant (user_id, tenant_id, mapping_status, mapped_period_year, mapped_period_month);
