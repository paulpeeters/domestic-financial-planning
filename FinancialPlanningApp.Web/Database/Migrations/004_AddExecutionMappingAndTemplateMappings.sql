ALTER TABLE payment_executions
ADD COLUMN execution_type VARCHAR(32) NULL,
ADD COLUMN mapping_status VARCHAR(32) NOT NULL DEFAULT 'UNMAPPED',
ADD COLUMN mapped_template_id BIGINT NULL,
ADD COLUMN mapped_period_year INT NULL,
ADD COLUMN mapped_period_month INT NULL,
ADD INDEX ix_payment_exec_mapping (user_id, mapping_status, mapped_period_year, mapped_period_month),
ADD CONSTRAINT fk_payment_exec_mapped_template FOREIGN KEY (mapped_template_id) REFERENCES recurring_payment_templates(id);

CREATE TABLE IF NOT EXISTS payment_template_mappings (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id BIGINT NOT NULL,
    execution_id BIGINT NOT NULL,
    template_id BIGINT NOT NULL,
    period_year INT NOT NULL,
    period_month INT NOT NULL,
    mapped_by VARCHAR(128) NOT NULL,
    mapped_utc DATETIME(6) NOT NULL,
    confidence_note VARCHAR(256) NULL,
    CONSTRAINT fk_ptm_user FOREIGN KEY (user_id) REFERENCES app_users(id),
    CONSTRAINT fk_ptm_execution FOREIGN KEY (execution_id) REFERENCES payment_executions(id),
    CONSTRAINT fk_ptm_template FOREIGN KEY (template_id) REFERENCES recurring_payment_templates(id),
    UNIQUE KEY ux_ptm_execution (execution_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
