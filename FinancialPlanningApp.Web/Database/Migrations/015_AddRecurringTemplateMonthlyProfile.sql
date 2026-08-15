ALTER TABLE recurring_payment_templates
ADD COLUMN amount_mode VARCHAR(40) NOT NULL DEFAULT 'Fixed' AFTER amount,
ADD COLUMN monthly_amounts_json JSON NULL AFTER amount_mode,
ADD COLUMN payment_lag_months INT NOT NULL DEFAULT 0 AFTER payment_day;
