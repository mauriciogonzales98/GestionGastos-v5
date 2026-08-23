import js from '@eslint/js';
import tseslint from 'typescript-eslint';
import reactHooks from 'eslint-plugin-react-hooks';

export default tseslint.config(
  { ignores: ['dist/', 'coverage/', 'node_modules/'] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    files: ['**/*.{ts,tsx}'],
    plugins: { 'react-hooks': reactHooks },
    rules: {
      ...reactHooks.configs.recommended.rules,
      // AGENTS.md: no `any`. Si es inevitable, va con un comentario que explique por qué —
      // y por eso es error, no warning: un warning no rompe la puerta.
      '@typescript-eslint/no-explicit-any': 'error',
    },
  },
);
