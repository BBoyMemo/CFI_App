import subprocess
import time
import urllib.request
import sys

# Try to open browser with the frontend URL
frontend_url = "http://localhost:5173"
backend_url = "http://localhost:5140"

try:
    # Test frontend
    response = urllib.request.urlopen(frontend_url, timeout=5)
    frontend_status = "✓ Frontend running on http://localhost:5173"
    print(frontend_status)
except Exception as e:
    print(f"✗ Frontend error: {e}")
    sys.exit(1)

try:
    # Test backend
    response = urllib.request.urlopen(backend_url, timeout=5)
except urllib.error.HTTPError as e:
    if e.code == 404:
        backend_status = "✓ Backend running on http://localhost:5140 (404 on root is normal)"
        print(backend_status)
    else:
        print(f"✗ Backend error: {e}")
except Exception as e:
    print(f"✗ Backend error: {e}")

# Try to open in default browser
try:
    import webbrowser
    webbrowser.open(frontend_url)
    print(f"✓ Opened {frontend_url} in default browser")
except Exception as e:
    print(f"Note: Could not open browser: {e}")

print("\n✓ Application is ready!")
print(f"Frontend: {frontend_url}")
print(f"Backend API: {backend_url}")
