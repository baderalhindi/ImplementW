import js from '@eslint/js';
import prettier from 'eslint-config-prettier';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['dist', 'node_modules', 'coverage', 'playwright-report', 'test-results'] },
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      ...tseslint.configs.strictTypeChecked,
      ...tseslint.configs.stylisticTypeChecked,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      ecmaVersion: 2023,
      globals: globals.browser,
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      '@typescript-eslint/consistent-type-imports': ['error', { fixStyle: 'inline-type-imports' }],
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
      'no-console': ['error', { allow: ['warn', 'error'] }],
      // TASK-077 (CTL-11): text reaches the page only as React text or attribute values, which React encodes. Nothing
      // parses a string as markup or code, and no link or source is bound to data (output-encoding-checklist.md).
      'no-eval': 'error',
      'no-new-func': 'error',
      'no-script-url': 'error',
      'no-restricted-syntax': [
        'error',
        {
          selector: 'JSXAttribute[name.name=/^(dangerouslySetInnerHTML|srcDoc)$/]',
          message:
            'Render text as JSX children; never hand React a string to parse as HTML (TASK-077).',
        },
        {
          selector: 'AssignmentExpression[left.property.name=/^(innerHTML|outerHTML)$/]',
          message: 'Set textContent or render JSX; never assign HTML (TASK-077).',
        },
        {
          selector:
            'CallExpression[callee.property.name=/^(insertAdjacentHTML|createContextualFragment|parseFromString)$/]',
          message: 'Never parse a string as HTML (TASK-077).',
        },
        {
          selector:
            "CallExpression[callee.object.name='document'][callee.property.name=/^write(ln)?$/]",
          message: 'Never write markup into the document (TASK-077).',
        },
        {
          selector:
            "JSXOpeningElement[name.name=/^[a-z]/] > JSXAttribute[name.name=/^(href|src|action|formAction)$/][value.type='JSXExpressionContainer']",
          message:
            'A link or source bound to a value can carry javascript: or another host. Use <Link to> with a path built from ids, checked with isAppPath (TASK-077).',
        },
      ],
    },
  },
  {
    files: ['eslint.config.js', 'vite.config.ts'],
    languageOptions: { globals: globals.node },
  },
  // TASK-085: the Playwright journeys run in Node. A fixture's `use` callback is Playwright's, not React's `use`.
  {
    files: ['e2e/**/*.{ts,mjs}'],
    languageOptions: { globals: globals.node },
    rules: { 'react-hooks/rules-of-hooks': 'off' },
  },
  prettier,
);
