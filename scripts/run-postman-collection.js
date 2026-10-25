#!/usr/bin/env node
/*
 * Runs a Postman collection against a running API and writes a readable evidence report.
 *
 * It is a tiny stand-in for `newman` that needs NO npm install - only Node 18+ (built-in fetch).
 * It understands what this repo's collection uses: {{variables}}, pre-request scripts, test scripts with
 * pm.test / pm.expect / pm.response / pm.collectionVariables. If you have Postman, simply import the
 * collection there instead; both give the same pass/fail picture.
 *
 *   node scripts/run-postman-collection.js [collection.json] [--baseUrl http://localhost:5282] [--report report.md]
 */
const fs = require('fs');
const path = require('path');
const vm = require('vm');

// ------------------------------------------------------------------ arguments
const args = process.argv.slice(2);
const option = (name) => {
  const index = args.indexOf(name);
  return index >= 0 ? args[index + 1] : undefined;
};
const collectionPath = args.find((a, i) => !a.startsWith('--') && !(i > 0 && args[i - 1].startsWith('--')))
  || path.join(__dirname, '..', 'postman', 'TrainingManagementApi.postman_collection.json');
const reportPath = option('--report');

const collection = JSON.parse(fs.readFileSync(collectionPath, 'utf8'));
const variables = Object.fromEntries((collection.variable || []).map((v) => [v.key, v.value]));
if (option('--baseUrl')) variables.baseUrl = option('--baseUrl');

// ------------------------------------------------------------------ helpers
const substitute = (text) =>
  String(text).replace(/\{\{([^}]+)\}\}/g, (match, name) => {
    if (name === '$timestamp') return String(Math.floor(Date.now() / 1000));
    if (name === '$randomInt') return String(Math.floor(Math.random() * 1000));
    return name in variables ? String(variables[name]) : match;
  });

const show = (value) => {
  try { return JSON.stringify(value); } catch { return String(value); }
};
const typeOf = (value) => (Array.isArray(value) ? 'array' : value === null ? 'null' : typeof value);
const isEmpty = (value) =>
  value == null || (typeof value === 'string' || Array.isArray(value) ? value.length === 0 : Object.keys(value).length === 0);

// A small subset of Chai (what Postman's pm.expect exposes), enough for the collection's tests.
function expect(actual, message) {
  const state = { negate: false };
  const check = (condition, description) => {
    if (state.negate ? condition : !condition) {
      throw new Error(`${message ? message + ': ' : ''}expected ${show(actual)} ${state.negate ? 'not ' : ''}${description}`);
    }
  };
  const compare = (name, test) => (value) => { check(test(actual, value), `to be ${name} ${show(value)}`); return proxy; };
  const proxy = new Proxy({}, {
    get(_, prop) {
      switch (prop) {
        case 'to': case 'be': case 'been': case 'is': case 'that': case 'which': case 'and': case 'have': case 'at': case 'of': case 'with':
          return proxy;
        case 'not': state.negate = !state.negate; return proxy;
        case 'empty': check(isEmpty(actual), 'to be empty'); return proxy;
        case 'ok': check(Boolean(actual), 'to be truthy'); return proxy;
        case 'a': case 'an': return (type) => { check(typeOf(actual) === type, `to be a ${type}`); return proxy; };
        case 'equal': case 'equals': case 'eq': return (value) => { check(actual === value, `to equal ${show(value)}`); return proxy; };
        case 'above': case 'greaterThan': return compare('above', (a, v) => a > v);
        case 'below': case 'lessThan': return compare('below', (a, v) => a < v);
        case 'least': return compare('at least', (a, v) => a >= v);
        case 'most': return compare('at most', (a, v) => a <= v);
        case 'include': case 'includes': case 'contain': case 'contains':
          return (value) => { check((typeof actual === 'string' || Array.isArray(actual)) && actual.includes(value), `to include ${show(value)}`); return proxy; };
        case 'property':
          return (name) => { check(actual != null && Object.prototype.hasOwnProperty.call(actual, name), `to have property ${show(name)}`); return proxy; };
        default: return undefined;
      }
    },
  });
  return proxy;
}

// ------------------------------------------------------------------ flatten the collection
const requests = [];
(function walk(items, folder) {
  for (const item of items) {
    if (item.item) walk(item.item, item.name);
    else requests.push({ folder, item });
  }
})(collection.item, '');

const scriptOf = (item, listen) =>
  (item.event || []).filter((e) => e.listen === listen).map((e) => e.script.exec.join('\n')).join('\n');

// ------------------------------------------------------------------ run
async function run() {
  const results = [];
  let passed = 0;
  let failed = 0;

  console.log(`Collection: ${collection.info.name}\nBase URL:   ${variables.baseUrl}\n`);

  for (const { folder, item } of requests) {
    const record = { folder, name: item.name, tests: [], method: item.request.method };
    const pmBase = {
      collectionVariables: { get: (k) => variables[k], set: (k, v) => { variables[k] = v; } },
      expect,
    };

    // Pre-request script
    const pre = scriptOf(item, 'prerequest');
    if (pre) vm.runInNewContext(pre, { pm: { ...pmBase, test: () => {} }, Date, Math });

    const url = substitute(item.request.url.raw);
    const headers = Object.fromEntries((item.request.header || []).map((h) => [h.key, substitute(h.value)]));
    const body = item.request.body && item.request.body.raw !== undefined ? substitute(item.request.body.raw) : undefined;
    record.url = url;
    record.requestBody = body;

    let response;
    let text = '';
    try {
      response = await fetch(url, { method: item.request.method, headers, body });
      text = await response.text();
    } catch (error) {
      record.error = `Request failed: ${error.message}`;
    }

    if (response) {
      record.status = response.status;
      record.responseBody = text;
      record.responseHeaders = Object.fromEntries(
        ['location', 'x-total-count', 'x-page', 'x-page-size', 'content-type'].filter((h) => response.headers.has(h)).map((h) => [h, response.headers.get(h)]));

      const pm = {
        ...pmBase,
        response: {
          code: response.status,
          json: () => JSON.parse(text),
          text: () => text,
          headers: { get: (name) => response.headers.get(name) },
          to: {
            have: {
              status: (code) => {
                if (response.status !== code) throw new Error(`expected response to have status code ${code} but got ${response.status}`);
              },
            },
          },
        },
        test: (name, fn) => {
          try { fn(); record.tests.push({ name, ok: true }); passed++; }
          catch (error) { record.tests.push({ name, ok: false, message: error.message }); failed++; }
        },
      };

      const tests = scriptOf(item, 'test');
      try {
        if (tests) vm.runInNewContext(tests, { pm, JSON, Number, Object, Math, Date });
      } catch (error) {
        record.tests.push({ name: 'Test script', ok: false, message: error.message });
        failed++;
      }
    } else {
      failed++;
    }

    const ok = !record.error && record.tests.every((t) => t.ok);
    console.log(`${ok ? 'PASS' : 'FAIL'}  ${record.method.padEnd(6)} ${item.name}  ->  ${record.status ?? record.error}`);
    record.tests.filter((t) => !t.ok).forEach((t) => console.log(`        x ${t.name}: ${t.message}`));
    if (record.error) console.log(`        x ${record.error}`);
    results.push(record);
  }

  console.log(`\n${results.length} requests, ${passed} tests passed, ${failed} failed`);
  if (reportPath) writeReport(results, passed, failed);
  process.exit(failed === 0 ? 0 : 1);
}

function writeReport(results, passed, failed) {
  const pretty = (text) => {
    try { return JSON.stringify(JSON.parse(text), null, 2); } catch { return text; }
  };
  const clip = (text, max = 1800) => (text.length > max ? text.slice(0, max) + '\n... (truncated)' : text);

  const out = [];
  out.push(`# API test evidence - ${collection.info.name}`);
  out.push('');
  out.push(`- Run: ${new Date().toISOString()}`);
  out.push(`- API: ${variables.baseUrl}`);
  out.push(`- Collection: \`${path.relative(process.cwd(), collectionPath)}\``);
  out.push(`- Result: **${results.length} requests, ${passed} tests passed, ${failed} failed**`);
  out.push('');
  out.push('Generated by `scripts/run-postman-collection.js`: every line below is a real HTTP call made to the running API.');
  out.push('');
  out.push('| # | Folder | Request | Status | Tests |');
  out.push('|---|---|---|---|---|');
  results.forEach((r, i) => {
    const okCount = r.tests.filter((t) => t.ok).length;
    out.push(`| ${i + 1} | ${r.folder} | ${r.name} | ${r.status ?? 'ERR'} | ${okCount}/${r.tests.length} ${r.tests.every((t) => t.ok) && !r.error ? 'passed' : 'FAILED'} |`);
  });
  out.push('');
  let lastFolder = '';
  results.forEach((r, i) => {
    if (r.folder !== lastFolder) { out.push(`## ${r.folder}`, ''); lastFolder = r.folder; }
    out.push(`### ${i + 1}. ${r.name}`, '');
    out.push(`\`${r.method} ${r.url}\` -> **${r.status ?? 'no response'}**`, '');
    if (r.requestBody) out.push('Request body:', '```json', r.requestBody, '```', '');
    if (Object.keys(r.responseHeaders || {}).length) out.push('Response headers: ' + Object.entries(r.responseHeaders).map(([k, v]) => `\`${k}: ${v}\``).join(', '), '');
    if (r.responseBody) out.push('Response body:', '```json', clip(pretty(r.responseBody)), '```', '');
    r.tests.forEach((t) => out.push(`- ${t.ok ? '[x]' : '[ ] FAILED'} ${t.name}${t.ok ? '' : ' - ' + t.message}`));
    if (r.error) out.push(`- [ ] FAILED ${r.error}`);
    out.push('');
  });

  fs.mkdirSync(path.dirname(path.resolve(reportPath)), { recursive: true });
  fs.writeFileSync(reportPath, out.join('\n'));
  console.log(`Report written to ${reportPath}`);
}

run().catch((error) => {
  console.error(error);
  process.exit(2);
});
