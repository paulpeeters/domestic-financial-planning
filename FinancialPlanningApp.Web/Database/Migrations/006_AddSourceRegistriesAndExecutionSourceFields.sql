CREATE TABLE IF NOT EXISTS registered_bank_accounts (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id BIGINT NOT NULL,
    display_name VARCHAR(128) NOT NULL,
    account_number VARCHAR(64) NOT NULL,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT fk_registered_bank_accounts_user FOREIGN KEY (user_id) REFERENCES app_users(id),
    UNIQUE KEY ux_registered_bank_accounts_user_account (user_id, account_number)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS registered_credit_cards (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id BIGINT NOT NULL,
    display_name VARCHAR(128) NOT NULL,
    card_number VARCHAR(64) NOT NULL,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT fk_registered_credit_cards_user FOREIGN KEY (user_id) REFERENCES app_users(id),
    UNIQUE KEY ux_registered_credit_cards_user_card (user_id, card_number)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

ALTER TABLE payment_executions
    ADD COLUMN source_sequence VARCHAR(64) NULL AFTER source_reference,
    ADD COLUMN source_account_number VARCHAR(64) NULL AFTER source_sequence,
    ADD COLUMN source_card_number VARCHAR(64) NULL AFTER source_account_number;

ALTER TABLE payment_executions
    ADD INDEX ix_payment_exec_source_ref (user_id, source_reference(191)),
    ADD INDEX ix_payment_exec_source_seq (user_id, source_type, source_sequence, source_account_number, source_card_number);
