#ifndef SPHEREINTERSECT_INCLUDED
#define SPHEREINTERSECT_INCLUDED

// From Inigo Quilez, https://www.iquilezles.org/www/articles/intersectors/intersectors.htm
void SphereIntersect_float(float3 rayDir, float3 spherePos, float radius, out float sphere)
{
    float3 oc = -spherePos;
    float b = dot(oc, rayDir);
    float c = dot(oc, oc) - radius * radius;
    float h = b * b - c;
    if(h < 0.0) sphere = -1.0;
    h = sqrt(h);
    sphere = -b - h;
}

#endif