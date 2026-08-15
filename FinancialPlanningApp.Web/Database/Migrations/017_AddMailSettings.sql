CREATE TABLE IF NOT EXISTS mail_settings (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    tenant_id BIGINT NULL,
    scope_key VARCHAR(64) NOT NULL,
    is_enabled TINYINT(1) NOT NULL DEFAULT 0,
    provider VARCHAR(32) NOT NULL DEFAULT 'Disabled',
    from_name VARCHAR(128) NULL,
    from_email VARCHAR(256) NULL,
    base_url VARCHAR(512) NULL,
    api_key VARCHAR(2048) NULL,
    smtp_host VARCHAR(256) NULL,
    smtp_port INT NULL,
    smtp_username VARCHAR(256) NULL,
    smtp_password VARCHAR(2048) NULL,
    smtp_use_ssl TINYINT(1) NOT NULL DEFAULT 1,
    updated_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT fk_mail_settings_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id),
    UNIQUE KEY ux_mail_settings_scope (scope_key),
    INDEX ix_mail_settings_tenant (tenant_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT INTO mail_settings(scope_key, tenant_id, is_enabled, provider, smtp_use_ssl)
SELECT 'global', NULL, 0, 'Disabled', 1
WHERE NOT EXISTS (
    SELECT 1 FROM mail_settings WHERE scope_key = 'global'
);
