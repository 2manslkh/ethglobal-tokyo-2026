import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name('infra.py')
PROJECT = 'tagtag-nft-staging-2026'


def configuration(**changes):
    values = {
        'project_id': PROJECT,
        'project_number': '542095619867',
        'region': 'asia-northeast1',
        'map_bucket': 'tagtag-nft-staging-2026-maps',
        'firebase_api_key': 'AIzaStagingExampleKeyForTests',
        'nft_enabled': False,
        'nft_contract_address': '',
        'nft_wallet_domain': '',
        'rpc_secret_version': '',
        'signer_secret_version': '',
    }
    values.update(changes)
    return values


def invoke(command, values, *extra):
    with tempfile.TemporaryDirectory() as directory:
        path = Path(directory) / 'staging.json'
        path.write_text(json.dumps(values), encoding='utf-8')
        return subprocess.run([sys.executable, str(SCRIPT), command, '--config', str(path), *extra],
                              text=True, capture_output=True, check=False)


class StagingInfrastructureTests(unittest.TestCase):
    def test_disabled_plan_is_scoped_and_cannot_schedule_minting(self):
        result = invoke('plan', configuration())
        self.assertEqual(result.returncode, 0, result.stderr)
        plan = result.stdout
        self.assertIn('gcloud builds submit backend', plan)
        self.assertIn('--project=tagtag-nft-staging-2026', plan)
        self.assertIn('--region=asia-northeast1', plan)
        self.assertIn('firebase deploy --only firestore:rules,firestore:indexes', plan)
        self.assertIn('GOOGLE_CLOUD_PROJECT=tagtag-nft-staging-2026', plan)
        self.assertIn('TAGTAG_MAP_BUCKET=tagtag-nft-staging-2026-maps', plan)
        self.assertIn('NFT_ENABLED=false', plan)
        self.assertIn('gcloud scheduler jobs pause tagtag-nft-mint', plan)
        self.assertIn('gcloud run deploy tagtag-api', plan)
        self.assertIn('FIREBASE_AUTH_DOMAIN=tagtag-nft-staging-2026.firebaseapp.com', plan)
        self.assertIn("'--schedule=* * * * *'", plan)
        self.assertIn('--tasks=1 --parallelism=1', plan)
        self.assertNotIn('NFT_SIGNER_PRIVATE_KEY=', plan)
        self.assertNotIn('tagtag-tokyo-2026', plan)

    def test_wrong_project_or_bucket_is_rejected_before_plan(self):
        for change in ({'project_id': 'tagtag-tokyo-2026'},
                       {'project_number': '329004805254'},
                       {'map_bucket': 'tagtag-tokyo-2026-maps'},
                       {'region': 'us-central1'}):
            with self.subTest(change=change):
                result = invoke('plan', configuration(**change))
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual(result.stdout, '')

    def test_enabled_plan_requires_complete_sepolia_configuration(self):
        incomplete = invoke('plan', configuration(nft_enabled=True))
        self.assertNotEqual(incomplete.returncode, 0)
        self.assertIn('nft_contract_address', incomplete.stderr)
        valid = configuration(
            nft_enabled=True,
            nft_contract_address='0x1111111111111111111111111111111111111111',
            nft_wallet_domain='tagtag-api-542095619867.asia-northeast1.run.app',
            rpc_secret_version='1', signer_secret_version='2')
        result = invoke('plan', valid)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('NFT_ENABLED=true', result.stdout)
        self.assertIn('NFT_SIGNER_PRIVATE_KEY=tagtag-staging-sepolia-signer:2', result.stdout)
        self.assertIn('NFT_RPC_URL=tagtag-staging-sepolia-rpc-url:1', result.stdout)
        self.assertIn('gcloud scheduler jobs resume tagtag-nft-mint', result.stdout)
        api_line = next(line for line in result.stdout.splitlines() if line.startswith('gcloud run deploy tagtag-api'))
        self.assertNotIn('NFT_SIGNER_PRIVATE_KEY', api_line)
        self.assertNotIn('NFT_RPC_URL', api_line)

    def test_enabled_plan_rejects_production_domain_mainnet_or_unversioned_secrets(self):
        base = configuration(nft_enabled=True, nft_contract_address='0x1111111111111111111111111111111111111111',
                             nft_wallet_domain='tagtag-api-542095619867.asia-northeast1.run.app',
                             rpc_secret_version='1', signer_secret_version='2')
        for change in ({'nft_wallet_domain': 'tagtag-api-329004805254.asia-northeast1.run.app'},
                       {'nft_wallet_domain': 'tagtag-api-123456789012.asia-northeast1.run.app'},
                       {'nft_chain_id': '1'}, {'signer_secret_version': 'latest'},
                       {'rpc_secret_version': 'latest'}):
            with self.subTest(change=change):
                result = invoke('plan', {**base, **change})
                self.assertNotEqual(result.returncode, 0)

    def test_apply_requires_explicit_staging_confirmation_before_cloud_access(self):
        result = invoke('apply', configuration())
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('confirm-project', result.stderr)
        self.assertEqual(result.stdout, '')

    def test_pause_enqueue_only_changes_api_flag_and_keeps_worker_running(self):
        result = invoke('plan-pause-enqueue', configuration())
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('gcloud run services update tagtag-api', result.stdout)
        self.assertIn('--update-env-vars=NFT_ENABLED=false', result.stdout)
        self.assertIn('--project=tagtag-nft-staging-2026', result.stdout)
        self.assertNotIn('scheduler jobs pause', result.stdout)
        self.assertNotIn('run jobs deploy', result.stdout)

    def test_generated_plan_is_valid_bash(self):
        plan = invoke('plan', configuration())
        self.assertEqual(plan.returncode, 0, plan.stderr)
        syntax = subprocess.run(['bash', '-n'], input=plan.stdout, text=True, capture_output=True)
        self.assertEqual(syntax.returncode, 0, syntax.stderr)

    def test_disabled_preflight_refuses_existing_enabled_worker(self):
        with tempfile.TemporaryDirectory() as directory:
            config = Path(directory) / 'staging.json'
            config.write_text(json.dumps(configuration()), encoding='utf-8')
            gcloud = Path(directory) / 'gcloud'
            gcloud.write_text('''#!/bin/sh
case "$1 $2 $3" in
  "projects describe tagtag-nft-staging-2026") echo '{"projectId":"tagtag-nft-staging-2026","projectNumber":"542095619867","lifecycleState":"ACTIVE"}' ;;
  "billing projects describe") echo '{"billingEnabled":true}' ;;
  "run jobs describe") echo '{"spec":{"template":{"spec":{"template":{"spec":{"containers":[{"env":[{"name":"NFT_ENABLED","value":"true"}]}]}}}}}}' ;;
  *) exit 4 ;;
esac
''', encoding='utf-8')
            gcloud.chmod(0o755)
            firebase = Path(directory) / 'firebase'
            firebase.write_text('''#!/bin/sh
echo '{"result":[{"projectId":"tagtag-nft-staging-2026"}]}'
''', encoding='utf-8')
            firebase.chmod(0o755)
            env = {**os.environ, 'PATH': f'{directory}:{os.environ["PATH"]}'}
            result = subprocess.run([sys.executable, str(SCRIPT), 'preflight', '--config', str(config)],
                                    text=True, capture_output=True, check=False, env=env)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('pending work', result.stderr)
            self.assertEqual(result.stdout, '')

    def test_preflight_reports_disabled_billing_before_firebase_login(self):
        with tempfile.TemporaryDirectory() as directory:
            config = Path(directory) / 'staging.json'
            config.write_text(json.dumps(configuration()), encoding='utf-8')
            gcloud = Path(directory) / 'gcloud'
            gcloud.write_text('''#!/bin/sh
case "$1 $2 $3" in
  "projects describe tagtag-nft-staging-2026") echo '{"projectId":"tagtag-nft-staging-2026","projectNumber":"542095619867","lifecycleState":"ACTIVE"}' ;;
  "config get-value project") echo tagtag-tokyo-2026 ;;
  "billing projects describe") echo '{"billingEnabled":false}' ;;
  *) exit 4 ;;
esac
''', encoding='utf-8')
            gcloud.chmod(0o755)
            env = {**os.environ, 'PATH': f'{directory}:{os.environ["PATH"]}'}
            result = subprocess.run([sys.executable, str(SCRIPT), 'preflight', '--config', str(config)],
                                    text=True, capture_output=True, check=False, env=env)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('billing is not enabled', result.stderr)
            self.assertEqual(result.stdout, '')


if __name__ == '__main__':
    unittest.main()
