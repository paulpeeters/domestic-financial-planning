SET @has_first_name := (
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'app_users' AND COLUMN_NAME = 'first_name'
);
SET @ddl_first_name := IF(@has_first_name = 0, 'ALTER TABLE app_users ADD COLUMN first_name VARCHAR(64) NULL AFTER email', 'SELECT 1');
PREPARE stmt1 FROM @ddl_first_name; EXECUTE stmt1; DEALLOCATE PREPARE stmt1;

SET @has_last_name := (
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'app_users' AND COLUMN_NAME = 'last_name'
);
SET @ddl_last_name := IF(@has_last_name = 0, 'ALTER TABLE app_users ADD COLUMN last_name VARCHAR(64) NULL AFTER first_name', 'SELECT 1');
PREPARE stmt2 FROM @ddl_last_name; EXECUTE stmt2; DEALLOCATE PREPARE stmt2;

SET @has_avatar_url := (
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'app_users' AND COLUMN_NAME = 'avatar_url'
);
SET @ddl_avatar_url := IF(@has_avatar_url = 0, 'ALTER TABLE app_users ADD COLUMN avatar_url VARCHAR(512) NULL AFTER last_name', 'SELECT 1');
PREPARE stmt3 FROM @ddl_avatar_url; EXECUTE stmt3; DEALLOCATE PREPARE stmt3;

SET @has_preferred_tenant_id := (
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'app_users' AND COLUMN_NAME = 'preferred_tenant_id'
);
SET @ddl_preferred_tenant_id := IF(@has_preferred_tenant_id = 0, 'ALTER TABLE app_users ADD COLUMN preferred_tenant_id BIGINT NULL AFTER is_global_admin', 'SELECT 1');
PREPARE stmt4 FROM @ddl_preferred_tenant_id; EXECUTE stmt4; DEALLOCATE PREPARE stmt4;

SET @has_short_name := (
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'tenants' AND COLUMN_NAME = 'short_name'
);
SET @ddl_short_name := IF(@has_short_name = 0, 'ALTER TABLE tenants ADD COLUMN short_name VARCHAR(10) NULL AFTER name', 'SELECT 1');
PREPARE stmt5 FROM @ddl_short_name; EXECUTE stmt5; DEALLOCATE PREPARE stmt5;

SET @has_fk_preferred := (
    SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'app_users' AND CONSTRAINT_NAME = 'fk_app_users_preferred_tenant'
);
SET @ddl_fk_preferred := IF(@has_fk_preferred = 0, 'ALTER TABLE app_users ADD CONSTRAINT fk_app_users_preferred_tenant FOREIGN KEY (preferred_tenant_id) REFERENCES tenants(id)', 'SELECT 1');
PREPARE stmt6 FROM @ddl_fk_preferred; EXECUTE stmt6; DEALLOCATE PREPARE stmt6;

SET @has_ix_preferred := (
    SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'app_users' AND INDEX_NAME = 'ix_app_users_preferred_tenant'
);
SET @ddl_ix_preferred := IF(@has_ix_preferred = 0, 'ALTER TABLE app_users ADD INDEX ix_app_users_preferred_tenant (preferred_tenant_id)', 'SELECT 1');
PREPARE stmt7 FROM @ddl_ix_preferred; EXECUTE stmt7; DEALLOCATE PREPARE stmt7;
