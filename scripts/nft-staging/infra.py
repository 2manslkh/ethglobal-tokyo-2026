#!/usr/bin/env python3
"""Plan or apply the isolated Tagtag NFT staging backend infrastructure.

No command reads secret payloads. `plan` and `validate` never contact Google Cloud.
"""

import argparse
import json
import re
import shlex
import subprocess
import sys
from pathlib import Path


PROJECT = 'tagtag-nft-staging-2026'
PROJECT_NUMBER = '542095619867'
REGION = 'asia-northeast1'
BUCKET = f'{PROJECT}-maps'
API = 'tagtag-api'
API_ACCOUNT = 'tagtag-nft-api'
CLEANUP = 'tagtag-nft-cleanup'
MINT = 'tagtag-nft-mint'
SCHEDULER = 'tagtag-nft-scheduler'
RPC_SECRET = 'tagtag-staging-sepolia-rpc-url'
SIGNER_SECRET = 'tagtag-staging-sepolia-signer'
ROOT = Path(__file__).resolve().parents[2]


def command(*parts):
    return ' '.join(shlex.quote(str(part)) for part in parts)


def load_config(path):
    config = json.loads(Path(path).read_text(encoding='utf-8'))
    if not isinstance(config, dict):
        raise ValueError('Configuration must be a JSON object')
    allowed = {'project_id', 'project_number', 'region', 'map_bucket', 'firebase_api_key',
               'nft_enabled', 'nft_chain_id', 'nft_contract_address', 'nft_wallet_domain',
               'rpc_secret_version', 'signer_secret_version'}
    extra = set(config) - allowed
    if extra:
        raise ValueError(f'Unknown configuration keys: {", ".join(sorted(extra))}')
    for key, expected in [('project_id', PROJECT), ('project_number', PROJECT_NUMBER),
                          ('region', REGION), ('map_bucket', BUCKET)]:
        if str(config.get(key, '')) != expected:
            raise ValueError(f'{key} must be {expected}')
    key = config.get('firebase_api_key')
    if not isinstance(key, str) or not re.fullmatch(r'AIza[A-Za-z0-9_-]{12,}', key):
        raise ValueError('firebase_api_key must be the staging Firebase web API key')
    if type(config.get('nft_enabled')) is not bool:
        raise ValueError('nft_enabled must be true or false')
    if str(config.get('nft_chain_id', '11155111')) != '11155111':
        raise ValueError('nft_chain_id must be Sepolia 11155111')
    if config['nft_enabled']:
        address = config.get('nft_contract_address', '')
        if not isinstance(address, str) or not re.fullmatch(r'0x[0-9a-fA-F]{40}', address) or int(address, 16) == 0:
            raise ValueError('nft_contract_address must be a nonzero Ethereum address')
        domain = config.get('nft_wallet_domain', '')
        if domain != f'{API}-{PROJECT_NUMBER}.{REGION}.run.app':
            raise ValueError('nft_wallet_domain must be the staging Cloud Run API host')
        for key in ('rpc_secret_version', 'signer_secret_version'):
            version = config.get(key)
            if not isinstance(version, str) or not re.fullmatch(r'[1-9][0-9]*', version):
                raise ValueError(f'{key} must be a pinned numeric Secret Manager version')
    return config


def account(name):
    return f'{name}@{PROJECT}.iam.gserviceaccount.com'


def render_plan(config):
    """Build a repeatable bash deployment script, with all resource names pinned here."""
    enabled = config['nft_enabled']
    api_account, cleanup_account, mint_account, scheduler_account = (
        account(name) for name in (API_ACCOUNT, CLEANUP, MINT, SCHEDULER))
    image = f'{REGION}-docker.pkg.dev/{PROJECT}/tagtag-nft/backend:staging'
    common = (f'GOOGLE_CLOUD_PROJECT={PROJECT},TAGTAG_MAP_BUCKET={BUCKET},'
              f'FIREBASE_API_KEY={config["firebase_api_key"]},'
              f'FIREBASE_AUTH_DOMAIN={PROJECT}.firebaseapp.com')
    nft = 'NFT_ENABLED=false,NFT_CHAIN_ID=11155111'
    if enabled:
        nft = (f'NFT_ENABLED=true,NFT_CHAIN_ID=11155111,'
               f'NFT_CONTRACT_ADDRESS={config["nft_contract_address"]},'
               f'NFT_WALLET_DOMAIN={config["nft_wallet_domain"]}')
    lines = ['#!/usr/bin/env bash', 'set -euo pipefail',
             '# This plan is scoped to one staging project. Apply runs a read-only preflight first.']
    lines.append(command('gcloud', 'services', 'enable', 'run.googleapis.com', 'cloudbuild.googleapis.com',
                         'artifactregistry.googleapis.com', 'firestore.googleapis.com',
                         'identitytoolkit.googleapis.com', 'iamcredentials.googleapis.com',
                         'secretmanager.googleapis.com', 'cloudscheduler.googleapis.com',
                         f'--project={PROJECT}'))

    def ensure(describe, create):
        lines.append(f'if ! {describe} >/dev/null 2>&1; then {create}; fi')

    ensure(command('gcloud', 'firestore', 'databases', 'describe', '--database=(default)', f'--project={PROJECT}'),
           command('gcloud', 'firestore', 'databases', 'create', '--database=(default)',
                   f'--location={REGION}', '--type=firestore-native', f'--project={PROJECT}'))
    ensure(command('gcloud', 'storage', 'buckets', 'describe', f'gs://{BUCKET}', f'--project={PROJECT}'),
           command('gcloud', 'storage', 'buckets', 'create', f'gs://{BUCKET}',
                   f'--location={REGION}', '--uniform-bucket-level-access',
                   '--public-access-prevention', f'--project={PROJECT}'))
    for name, email in ((API_ACCOUNT, api_account), (CLEANUP, cleanup_account),
                        (MINT, mint_account), (SCHEDULER, scheduler_account)):
        ensure(command('gcloud', 'iam', 'service-accounts', 'describe', email, f'--project={PROJECT}'),
               command('gcloud', 'iam', 'service-accounts', 'create', name, f'--project={PROJECT}'))
    ensure(command('gcloud', 'iam', 'roles', 'describe', 'tagtagAuthGet', f'--project={PROJECT}'),
           command('gcloud', 'iam', 'roles', 'create', 'tagtagAuthGet',
                   '--permissions=firebaseauth.users.get', '--stage=GA', f'--project={PROJECT}'))
    lines.append(command('gcloud', 'iam', 'roles', 'update', 'tagtagAuthGet',
                         '--permissions=firebaseauth.users.get', f'--project={PROJECT}'))
    for email in (api_account, cleanup_account, mint_account):
        lines.append(command('gcloud', 'projects', 'add-iam-policy-binding', PROJECT,
                             f'--member=serviceAccount:{email}', '--role=roles/datastore.user',
                             '--condition=None', '--quiet'))
    for email in (api_account, cleanup_account):
        lines.append(command('gcloud', 'projects', 'add-iam-policy-binding', PROJECT,
                             f'--member=serviceAccount:{email}',
                             f'--role=projects/{PROJECT}/roles/tagtagAuthGet', '--condition=None', '--quiet'))
        lines.append(command('gcloud', 'storage', 'buckets', 'add-iam-policy-binding', f'gs://{BUCKET}',
                             f'--member=serviceAccount:{email}', '--role=roles/storage.objectAdmin',
                             f'--project={PROJECT}'))
    lines.append(command('gcloud', 'iam', 'service-accounts', 'add-iam-policy-binding', api_account,
                         f'--member=serviceAccount:{api_account}',
                         '--role=roles/iam.serviceAccountTokenCreator', f'--project={PROJECT}'))
    if enabled:
        lines.append(command('gcloud', 'secrets', 'add-iam-policy-binding', SIGNER_SECRET,
                             f'--member=serviceAccount:{mint_account}',
                             '--role=roles/secretmanager.secretAccessor', f'--project={PROJECT}'))
        lines.append(command('gcloud', 'secrets', 'add-iam-policy-binding', RPC_SECRET,
                             f'--member=serviceAccount:{mint_account}',
                             '--role=roles/secretmanager.secretAccessor', f'--project={PROJECT}'))
    lines.append(command('firebase', 'deploy', '--only', 'firestore:rules,firestore:indexes',
                         '--project', PROJECT, '--non-interactive'))
    ensure(command('gcloud', 'artifacts', 'repositories', 'describe', 'tagtag-nft',
                   f'--location={REGION}', f'--project={PROJECT}'),
           command('gcloud', 'artifacts', 'repositories', 'create', 'tagtag-nft',
                   '--repository-format=docker', f'--location={REGION}', f'--project={PROJECT}'))
    lines.append(command('gcloud', 'builds', 'submit', 'backend', f'--tag={image}', f'--project={PROJECT}'))
    lines.append(command('gcloud', 'run', 'deploy', API, f'--image={image}', f'--region={REGION}',
                         f'--project={PROJECT}', f'--service-account={api_account}',
                         '--min-instances=0', '--max-instances=1', '--allow-unauthenticated',
                         f'--set-env-vars={common},{nft}', '--clear-secrets', '--quiet'))
    lines.append(command('gcloud', 'run', 'jobs', 'deploy', CLEANUP, f'--image={image}',
                         f'--region={REGION}', f'--project={PROJECT}',
                         f'--service-account={cleanup_account}', '--tasks=1', '--parallelism=1',
                         '--max-retries=0', '--command=node', '--args=src/cleanup.js',
                         f'--set-env-vars={common}', '--clear-secrets', '--quiet'))
    mint_parts = ['gcloud', 'run', 'jobs', 'deploy', MINT, f'--image={image}',
                  f'--region={REGION}', f'--project={PROJECT}', f'--service-account={mint_account}',
                  '--tasks=1', '--parallelism=1', '--max-retries=0', '--task-timeout=10m',
                  '--command=node', '--args=src/mint-worker.js',
                  f'--set-env-vars={common},{nft}']
    if enabled:
        mint_parts.append(f'--set-secrets=NFT_SIGNER_PRIVATE_KEY={SIGNER_SECRET}:{config["signer_secret_version"]},'
                          f'NFT_RPC_URL={RPC_SECRET}:{config["rpc_secret_version"]}')
    else:
        mint_parts.append('--clear-secrets')
    lines.append(command(*mint_parts, '--quiet'))
    for job in (CLEANUP, MINT):
        lines.append(command('gcloud', 'run', 'jobs', 'add-iam-policy-binding', job,
                             f'--member=serviceAccount:{scheduler_account}',
                             '--role=roles/run.invoker', f'--region={REGION}', f'--project={PROJECT}'))
        schedule = '0 * * * *' if job == CLEANUP else '* * * * *'
        target = f'https://run.googleapis.com/v2/projects/{PROJECT}/locations/{REGION}/jobs/{job}:run'
        description = command('gcloud', 'scheduler', 'jobs', 'describe', job,
                              f'--location={REGION}', f'--project={PROJECT}')
        options = (f'--location={REGION}', f'--project={PROJECT}', f'--schedule={schedule}',
                   f'--uri={target}', '--http-method=POST',
                   f'--oauth-service-account-email={scheduler_account}',
                   '--time-zone=Etc/UTC')
        create = command('gcloud', 'scheduler', 'jobs', 'create', 'http', job, *options)
        update = command('gcloud', 'scheduler', 'jobs', 'update', 'http', job, *options)
        lines.append(f'if {description} >/dev/null 2>&1; then {update}; else {create}; fi')
    state = command('gcloud', 'scheduler', 'jobs', 'describe', MINT,
                    f'--location={REGION}', f'--project={PROJECT}', '--format=value(state)')
    action = 'resume' if enabled else 'pause'
    expected = 'PAUSED' if enabled else 'ENABLED'
    lines.append(f'if [[ "$({state})" == {expected} ]]; then '
                 f'{command("gcloud", "scheduler", "jobs", action, MINT, f"--location={REGION}", f"--project={PROJECT}")}; fi')
    lines.append('# Confirm staging Firebase Auth providers and API key restrictions in Firebase Console.')
    return '\n'.join(lines) + '\n'


def render_pause_enqueue():
    return command('gcloud', 'run', 'services', 'update', API, f'--region={REGION}',
                   f'--project={PROJECT}', '--update-env-vars=NFT_ENABLED=false', '--quiet') + '\n'


def read_json(*args):
    try:
        result = subprocess.run(args, cwd=ROOT, capture_output=True, text=True, check=True)
    except FileNotFoundError as error:
        raise ValueError(f'{args[0]} CLI is unavailable; install and authenticate it before deployment') from error
    except subprocess.CalledProcessError as error:
        detail = error.stderr.strip().splitlines()[-1] if error.stderr.strip() else 'command failed'
        raise ValueError(f'{args[0]} read-only preflight failed: {detail}') from error
    return json.loads(result.stdout)


def existing_mint_job():
    result = subprocess.run(['gcloud', 'run', 'jobs', 'describe', MINT,
                             f'--region={REGION}', f'--project={PROJECT}', '--format=json'],
                            cwd=ROOT, capture_output=True, text=True, check=False)
    if result.returncode:
        if 'NOT_FOUND' in result.stderr or 'not found' in result.stderr.lower():
            return None
        raise ValueError('Cannot inspect existing mint job; disabled deploy is refused')
    return json.loads(result.stdout)


def nft_flags(value):
    if isinstance(value, dict):
        if value.get('name') == 'NFT_ENABLED':
            yield value.get('value')
        for child in value.values():
            yield from nft_flags(child)
    elif isinstance(value, list):
        for child in value:
            yield from nft_flags(child)


def preflight(config):
    """Read only checks before executing a plan; deny absent billing or wrong identity."""
    project = read_json('gcloud', 'projects', 'describe', PROJECT, '--format=json')
    if project.get('projectId') != PROJECT or str(project.get('projectNumber')) != PROJECT_NUMBER:
        raise ValueError('Google Cloud project ID or number does not match staging')
    if project.get('lifecycleState') != 'ACTIVE':
        raise ValueError('Staging Google Cloud project is not active')
    billing = read_json('gcloud', 'billing', 'projects', 'describe', PROJECT, '--format=json')
    if billing.get('billingEnabled') is not True:
        raise ValueError('Staging billing is not enabled; no deployment is allowed')
    projects = read_json('firebase', 'projects:list', '--json')
    if PROJECT not in {item.get('projectId') for item in projects.get('result', [])}:
        raise ValueError('Staging project has not been added to Firebase')
    if not config['nft_enabled']:
        mint_job = existing_mint_job()
        if mint_job is not None and set(nft_flags(mint_job)) != {'false'}:
            raise ValueError('Existing mint job may have pending work; use pause-enqueue, not disabled apply')
    if config['nft_enabled']:
        for secret, version in ((SIGNER_SECRET, config['signer_secret_version']),
                                (RPC_SECRET, config['rpc_secret_version'])):
            metadata = read_json('gcloud', 'secrets', 'versions', 'describe', version,
                                 f'--secret={secret}', f'--project={PROJECT}', '--format=json')
            if metadata.get('state') != 'ENABLED':
                raise ValueError(f'{secret} version {version} is not enabled')
        policy = read_json('gcloud', 'secrets', 'get-iam-policy', SIGNER_SECRET,
                           f'--project={PROJECT}', '--format=json')
        accessors = {member for binding in policy.get('bindings', [])
                     if binding.get('role') == 'roles/secretmanager.secretAccessor'
                     for member in binding.get('members', [])}
        allowed = {f'serviceAccount:{account(MINT)}'}
        if accessors - allowed:
            raise ValueError('Signer secret has accessors other than the mint worker')
        project_policy = read_json('gcloud', 'projects', 'get-iam-policy', PROJECT, '--format=json')
        if any(binding.get('role') == 'roles/secretmanager.secretAccessor'
               for binding in project_policy.get('bindings', [])):
            raise ValueError('Project-wide Secret Manager accessor grant prevents signer isolation')
        service = read_json('gcloud', 'run', 'services', 'describe', API,
                            f'--region={REGION}', f'--project={PROJECT}', '--format=json')
        from urllib.parse import urlparse
        observed = urlparse(service.get('status', {}).get('url', '')).hostname
        if observed != config['nft_wallet_domain']:
            raise ValueError('nft_wallet_domain does not match the deployed staging API host')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=('validate', 'plan', 'preflight', 'apply',
                                           'plan-pause-enqueue', 'pause-enqueue'))
    parser.add_argument('--config', required=True)
    parser.add_argument('--confirm-project', default='')
    args = parser.parse_args()
    try:
        config = load_config(args.config)
        if args.action == 'validate':
            print('Staging configuration is valid')
        elif args.action == 'plan':
            print(render_plan(config), end='')
        elif args.action == 'plan-pause-enqueue':
            print(render_pause_enqueue(), end='')
        elif args.action == 'pause-enqueue':
            if args.confirm_project != PROJECT:
                raise ValueError(f'pause-enqueue requires --confirm-project {PROJECT}')
            project = read_json('gcloud', 'projects', 'describe', PROJECT, '--format=json')
            if project.get('projectId') != PROJECT or str(project.get('projectNumber')) != PROJECT_NUMBER:
                raise ValueError('Google Cloud project number does not match staging')
            subprocess.run(['gcloud', 'run', 'services', 'update', API, f'--region={REGION}',
                            f'--project={PROJECT}', '--update-env-vars=NFT_ENABLED=false', '--quiet'],
                           cwd=ROOT, check=True)
        else:
            if args.action == 'apply' and args.confirm_project != PROJECT:
                raise ValueError(f'apply requires --confirm-project {PROJECT}')
            preflight(config)
            if args.action == 'preflight':
                print('Staging project and billing preflight passed')
            else:
                subprocess.run(['bash', '-euo', 'pipefail'], input=render_plan(config),
                               text=True, cwd=ROOT, check=True)
    except (OSError, ValueError, json.JSONDecodeError, subprocess.CalledProcessError) as error:
        print(f'Infrastructure tooling stopped: {error}', file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
