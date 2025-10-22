import * as assert from 'assert';

// You can import and use all API from the 'vscode' module
// as well as import your extension to test it
import * as vscode from 'vscode';
import { extractSchemaAndEntity } from '../commands/sql-commands';
// import * as myExtension from '../../extension';

suite('Extension Test Suite', () => {
	vscode.window.showInformationMessage('Start all tests.');

	test('Sample test', () => {
		assert.strictEqual(-1, [1, 2, 3].indexOf(5));
		assert.strictEqual(-1, [1, 2, 3].indexOf(0));
	});
});

suite('SQL Commands Test Suite', () => {
	test('Extract schema and entity from Tables path - Windows style', () => {
		const filePath = 'C:\\Projects\\DB\\dbo\\Tables\\Customer.sql';
		const result = extractSchemaAndEntity(filePath);
		assert.strictEqual(result, 'dbo.Customer');
	});

	test('Extract schema and entity from Tables path - Unix style', () => {
		const filePath = '/home/user/Projects/DB/dbo/Tables/Customer.sql';
		const result = extractSchemaAndEntity(filePath);
		assert.strictEqual(result, 'dbo.Customer');
	});

	test('Extract schema and entity from Stored Procedures path - Windows style', () => {
		const filePath = 'C:\\Projects\\DB\\dbo\\Stored Procedures\\zgen_Customer_GetById.sql';
		const result = extractSchemaAndEntity(filePath);
		assert.strictEqual(result, 'dbo.zgen_Customer_GetById');
	});

	test('Extract schema and entity from Stored Procedures path - Unix style', () => {
		const filePath = '/home/user/Projects/DB/dbo/Stored Procedures/zgen_Customer_GetById.sql';
		const result = extractSchemaAndEntity(filePath);
		assert.strictEqual(result, 'dbo.zgen_Customer_GetById');
	});

	test('Extract schema and entity with custom schema', () => {
		const filePath = '/home/user/Projects/DB/custom/Tables/Order.sql';
		const result = extractSchemaAndEntity(filePath);
		assert.strictEqual(result, 'custom.Order');
	});

	test('Extract schema and entity - default to dbo when no Tables or Stored Procedures folder', () => {
		const filePath = '/home/user/Projects/DB/SomeFile.sql';
		const result = extractSchemaAndEntity(filePath);
		assert.strictEqual(result, 'dbo.SomeFile');
	});
});
